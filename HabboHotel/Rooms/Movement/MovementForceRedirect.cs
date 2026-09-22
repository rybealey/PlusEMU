using System.Drawing;
using NLog;

namespace Plus.HabboHotel.Rooms.Movement;

/// <summary>
/// pixelrp Movement V2: reproduce the near-boundary redirect hitch on demand.
///
/// THE ONE MOVEMENT COMMAND THAT IS NOT READ-ONLY, and the header of every
/// other one says so for a reason. :movementstats, :movementphase,
/// :movementtrace and :movementreplan all observe. This one INJECTS - it issues
/// a redirect the player never clicked, at a moment they could not have clicked
/// it. Armed deliberately, on one named unit, for a bounded number of runs, and
/// it disarms itself at the end of them.
///
/// WHY IT HAS TO EXIST. The hitch needs a click inside a very narrow window
/// before a 500ms boundary - roughly a 1-in-10 chance per direction change, and
/// latency-dependent on top of that. The measured minimum was 2ms
/// (minRedirectMarginMs=2, 51 redirects inside 50ms). Normal play hits it
/// several times an hour and can never repeat it on purpose. Every existing
/// tool is built to CATCH that when luck provides it; none can CAUSE it. Until
/// something can, "did the fix work?" is answered by waiting to stop seeing a
/// thing that was always intermittent.
///
/// WHAT IT DOES NOT DO. It does not reach into the engine, fabricate an edge,
/// or take a shortcut to the interesting state. It waits for the real
/// precondition, picks a real tile, and calls MovementController.Redirect - the
/// same entry point MovementV2Bridge.RequestMove calls for a click, with the
/// same TraverseContext, under the same lock, followed by the same Signal. The
/// ONLY thing it supplies that a player cannot is the timing.
///
/// THE TIMING IS AIMED, MEASURED AND REPORTED - never assumed. The scheduler
/// thread wakes on a 1-15ms clamp and cannot be asked for a 5ms appointment, so
/// this runs its own thread, sleeps to just short of the target and spins the
/// last couple of milliseconds. Lock acquisition can still cost it the window.
/// Every attempt therefore logs the margin it ACTUALLY achieved beside the one
/// it wanted, and an attempt that lands outside tolerance is reported as LATE
/// rather than quietly counted as a hit. A harness that lied about its own
/// timing would be worse than no harness.
///
/// NOTHING BLOCKS OR LOGS INSIDE MovementLock. That is the lock the scheduler
/// takes to move every walker in the room; a diagnostic that slept or wrote a
/// log line while holding it would stall the very thing it is measuring, and
/// stall it only while armed - the worst possible shape for a measurement. The
/// lock is taken to read a few numbers and to make the one Redirect call, and
/// every wait and every log line happens after it has been let go.
///
/// Records go to the emulator console log tagged [MV2/force], like
/// [MV2/replan]. Arm :movementreplan alongside this and each injected redirect
/// gets its own A_ACTIVE / B_LATE verdict, which is the pairing the whole
/// exercise is for.
/// </summary>
public static class MovementForceRedirect
{
    private static readonly Logger Log = LogManager.GetLogger("MovementV2");

    /// <summary>
    /// How far before the boundary to aim, in milliseconds. 5 sits between the
    /// 2ms minimum actually observed on beta and the ~1 network trip below
    /// which the corrected packet provably cannot win the race.
    /// </summary>
    public const int DefaultMarginMs = 5;

    /// <summary>Attempts before the harness disarms itself.</summary>
    public const int DefaultRuns = 20;

    /// <summary>Ceiling on the run count, so a typo cannot arm this all night.</summary>
    public const int MaxRuns = 200;

    /// <summary>Ceiling on the aimed margin. Past this it is not the race any more.</summary>
    public const int MaxMarginMs = 250;

    /// <summary>
    /// How far the achieved margin may drift from the aimed one before the
    /// attempt is reported as LATE. Covers the scheduler's own TickSlackMs plus
    /// an uncontended lock; anything worse is a miss worth seeing.
    /// </summary>
    public const int ToleranceMs = 3;

    /// <summary>Spin, rather than sleep, for this last stretch before firing.</summary>
    private const int SpinLeadMs = 2;

    /// <summary>Idle poll while waiting for the precondition to come true.</summary>
    private const int PollIdleMs = 20;

    /// <summary>
    /// Minimum gap between two attempts. Half an interval, so one armed run can
    /// never fire twice inside a single edge and read as two samples of what was
    /// really one.
    /// </summary>
    private const int MinGapMs = 250;

    /// <summary>
    /// How far along the chosen direction the forced destination is pushed.
    ///
    /// THE EDGE UNDER TEST IS UNAFFECTED BY THIS, which is the only reason it
    /// is allowed to exist. Redirect plans from the same origin either way, so
    /// the geometry restaged at e+1 is the adjacent tile whatever the
    /// destination is; extending changes nothing about the experiment.
    ///
    /// What it changes is whether there is a next boundary to fire on. A
    /// redirect to the adjacent tile ENDS the walk one step later, the walker
    /// goes Standing, and the precondition is gone until somebody clicks again
    /// - which would make "20 runs" mean 20 manual clicks rather than a harness
    /// that runs on its own. Four tiles is enough to keep the session alive
    /// through the gap without steering the avatar across the room.
    /// </summary>
    private const int ExtendTiles = 4;

    private static readonly object Gate = new();
    private static readonly ManualResetEventSlim Wake = new(false);

    private static volatile bool _enabled;
    private static volatile bool _running;
    private static Thread? _thread;

    private static uint _roomId;
    private static int _virtualId;
    private static int _marginMs = DefaultMarginMs;
    private static int _runsRequested = DefaultRuns;

    private static int _attempts;
    private static int _fired;
    private static int _late;
    private static int _refused;
    private static int _missed;
    private static long _lastAttemptTick;

    public static bool Enabled => _enabled;

    public static string Stats() =>
        $"attempts={_attempts}/{_runsRequested} fired={_fired} late={_late} " +
        $"refused={_refused} missed={_missed}";

    /// <summary>
    /// Arm on one unit in one room. Replaces any previous arming outright -
    /// there is deliberately no way to have two of these running, because two
    /// injectors in one room would each be reading the other's damage.
    /// </summary>
    public static void Arm(uint roomId, int virtualId, string unitName, int marginMs, int runs)
    {
        lock (Gate)
        {
            _roomId = roomId;
            _virtualId = virtualId;
            _marginMs = Math.Clamp(marginMs, 0, MaxMarginMs);
            _runsRequested = Math.Clamp(runs, 1, MaxRuns);
            _attempts = 0;
            _fired = 0;
            _late = 0;
            _refused = 0;
            _missed = 0;
            // NOT long.MinValue, which is the obvious way to say "never" and is
            // wrong. The gap check is a SUBTRACTION - now - _lastAttemptTick -
            // and subtracting long.MinValue overflows in an unchecked context,
            // wrapping to a large NEGATIVE number. That reads as less than
            // MinGapMs, so the check returns at every cycle and the harness can
            // never fire at all. One interval back from now says the same thing
            // and says it in numbers the subtraction can hold.
            _lastAttemptTick = MovementScheduler.Instance.Clock.NowMs - MinGapMs;
            _enabled = true;

            EnsureThread();
        }

        Log.Info($"[MV2/force] ARMED unit={virtualId} ({unitName}) room={roomId} " +
                 $"margin={_marginMs}ms runs={_runsRequested} tolerance={ToleranceMs}ms");
        Wake.Set();
    }

    /// <summary>Stop, and say why in the log so a self-disarm is never a silence.</summary>
    public static void Disarm(string reason)
    {
        if (!_enabled)
            return;
        _enabled = false;
        Log.Info($"[MV2/force] DISARMED ({reason}) {Stats()}");
        Wake.Set();
    }

    /// <summary>
    /// One thread, created on the first arming and kept for the life of the
    /// process. It idles on <see cref="Wake"/> while disarmed rather than
    /// exiting, because a thread that exits on disarm races the next arming:
    /// EnsureThread would see the old one still alive, start nothing, and the
    /// harness would sit there armed and silent.
    /// </summary>
    private static void EnsureThread()
    {
        if (_thread is { IsAlive: true })
            return;

        _running = true;
        // ITS OWN THREAD, at default priority, and never the game loop. Blocking
        // anything on the loop that ticks every room and every client starves
        // the ThreadPool until room ticks are not SCHEDULED - which is the
        // freeze this codebase has already been burned by. A harness whose whole
        // job is to sleep until a precise moment is exactly the wrong tenant for
        // that thread.
        _thread = new Thread(Loop)
        {
            Name = "MV2-ForceRedirect",
            IsBackground = true
        };
        _thread.Start();
    }

    private static void Loop()
    {
        while (_running)
        {
            if (!_enabled)
            {
                Wake.Wait(250);
                Wake.Reset();
                continue;
            }

            try
            {
                Step();
            }
            catch (Exception ex)
            {
                // One bad cycle must not take the harness - or anything else -
                // down. Report and keep going; the run counter still bounds it.
                Log.Error($"[MV2/force] cycle faulted: {ex}");
                Thread.Sleep(PollIdleMs);
            }
        }
    }

    /// <summary>
    /// One cycle: find the appointment, keep it, fire. Split from the loop so
    /// every early return is a plain "try again shortly" rather than a flag.
    /// </summary>
    private static void Step()
    {
        if (_attempts >= _runsRequested)
        {
            Disarm("run count reached");
            return;
        }

        if (!MovementRegistry.TryGet(_roomId, out var room) || room == null || room.Closed)
        {
            Disarm("room closed or gone");
            return;
        }

        var clock = MovementScheduler.Instance.Clock;
        var now = clock.NowMs;

        if (now - _lastAttemptTick < MinGapMs)
        {
            Thread.Sleep(PollIdleMs);
            return;
        }

        // Three numbers read under the lock, and nothing else done under it.
        long fireAt = 0;
        var ready = false;
        lock (room.MovementLock)
        {
            if (!room.Closed
                && room.States.TryGetValue(_virtualId, out var w)
                && w != null
                && IsCandidate(room, w, out _))
            {
                // The appointment: margin before the boundary of the edge a
                // redirect decided now would restage. e+1 is the index Redirect
                // plans from, and the client begins drawing it from lookahead
                // the instant its cycleStart passes - which is the race.
                var e = w.ElapsingEdgeIndex(now);
                fireAt = w.EdgeStartTick(e + 1) - _marginMs;
                ready = true;
            }
        }

        if (!ready)
        {
            Thread.Sleep(PollIdleMs);
            return;
        }

        var lead = fireAt - clock.NowMs;
        if (lead <= 0)
        {
            // Already inside or past the window for this edge. Wait for the next
            // boundary rather than firing late on this one.
            Thread.Sleep(Math.Max(1, SpinLeadMs));
            return;
        }
        if (lead > 4 * MovementSettings.IntervalMs)
        {
            // Nonsense lead - a stale timeline, or the unit stopped between the
            // read and here. Re-read rather than sleeping on it.
            Thread.Sleep(PollIdleMs);
            return;
        }

        SleepUntil(clock, fireAt);
        Fire(room, fireAt);
    }

    /// <summary>
    /// Sleep to just short of the target, then spin. Thread.Sleep cannot be
    /// trusted to the millisecond on Windows, so the last <see cref="SpinLeadMs"/>
    /// is burned rather than slept - it is a couple of milliseconds on a
    /// dedicated background thread, and it is the whole point of the exercise.
    ///
    /// The clock behind this is Stopwatch-backed, so a millisecond appointment
    /// is a real thing to ask for. Against TickCount64's ~15.6ms granularity it
    /// would not have been.
    /// </summary>
    private static void SleepUntil(IMovementClock clock, long targetMs)
    {
        var coarse = targetMs - SpinLeadMs - clock.NowMs;
        if (coarse > 0)
            Thread.Sleep((int)Math.Min(coarse, int.MaxValue));

        while (clock.NowMs < targetMs)
        {
            if (!_enabled || !_running)
                return;
            Thread.SpinWait(40);
        }
    }

    /// <summary>
    /// Take the lock, make the attempt, let the lock go, then log and signal.
    /// The split exists so that neither the log write nor the scheduler wake
    /// happens while the room is held.
    /// </summary>
    private static void Fire(RoomMovement room, long plannedFireAt)
    {
        string line;
        bool accepted;

        lock (room.MovementLock)
            line = Attempt(room, plannedFireAt, out accepted);

        // Exactly as a click wakes it, and only when something was actually
        // staged - a refused or missed attempt changed nothing to flush.
        if (accepted)
            MovementScheduler.Instance.Signal(room);

        Log.Info(line);
    }

    /// <summary>
    /// The critical section. Everything is re-read here rather than carried in
    /// from the earlier read, because the scheduler has been running the whole
    /// time this thread was asleep and the walker may have stopped, turned, been
    /// paired or left the room.
    ///
    /// Returns the line to log; writes none itself.
    ///
    /// Caller MUST hold MovementLock.
    /// </summary>
    private static string Attempt(RoomMovement room, long plannedFireAt, out bool accepted)
    {
        accepted = false;

        var now = MovementScheduler.Instance.Clock.NowMs;
        _attempts++;
        _lastAttemptTick = now;

        if (room.Closed || !room.States.TryGetValue(_virtualId, out var w) || w == null)
        {
            _missed++;
            return $"[MV2/force] #{_attempts} MISS reason=unit-gone";
        }

        if (!IsCandidate(room, w, out var holder))
        {
            _missed++;
            return $"[MV2/force] #{_attempts} MISS reason=precondition-lost " +
                   $"mode={w.Mode} phase={w.LastPhaseDecision}";
        }

        var map = room.Room.GetGameMap();
        if (map == null)
        {
            _missed++;
            return $"[MV2/force] #{_attempts} MISS reason=no-map";
        }

        var user = room.Room.GetRoomUserManager()?.GetRoomUserByVirtualId(_virtualId);
        var ctx = MovementWalkerContext.For(user);

        var e = w.ElapsingEdgeIndex(now);
        var boundary = w.EdgeStartTick(e + 1);
        var actualMargin = boundary - now;

        // The origin Redirect will plan from, so the tile has to be chosen
        // around this rather than around where the avatar is standing.
        var origin = w.EdgeTo;
        var wouldBe = w.Route.HasNext ? w.Route.PeekNext() : origin;

        if (!TryPickDivergentTile(map, w, holder, origin, wouldBe, ctx, out var firstStep, out var target))
        {
            _missed++;
            return $"[MV2/force] #{_attempts} MISS reason=no-divergent-tile " +
                   $"origin={origin.X},{origin.Y}";
        }

        var sessionId = w.WalkSessionId;
        var revBefore = w.RouteRevision;

        // THE NORMAL PATH. Same call, same context, same lock as a click.
        //
        // The achieved margin is latched onto the walker for the duration of
        // this ONE call, so the corrected-edge packet it stages carries the
        // number to the browser and nothing else does. Cleared in a finally
        // because a Redirect that throws must not leave the next click looking
        // like a forced one.
        w.ForcedRedirectMarginMs = (int)actualMargin;
        try
        {
            accepted = MovementController.Redirect(room, w, target, ctx, now);
        }
        finally
        {
            w.ForcedRedirectMarginMs = MovementState.NotForced;
        }

        string verdict;
        if (!accepted)
        {
            _refused++;
            verdict = "REFUSED";
        }
        else if (Math.Abs(actualMargin - _marginMs) > ToleranceMs)
        {
            _late++;
            verdict = "LATE";
        }
        else
        {
            _fired++;
            verdict = "FIRED";
        }

        return
            $"[MV2/force] #{_attempts} {verdict} unit={_virtualId} " +
            $"targetMargin={_marginMs}ms actualMargin={actualMargin}ms " +
            $"aimError={now - plannedFireAt}ms " +
            $"edge={e}->{e + 1} boundary={boundary} " +
            $"origin={origin.X},{origin.Y} wouldBe={wouldBe.X},{wouldBe.Y} " +
            $"forcedStep={firstStep.X},{firstStep.Y} forcedTo={target.X},{target.Y} " +
            $"session={sessionId} rev={revBefore}->{w.RouteRevision} " +
            $"interval={w.IntervalMs}ms " +
            $"holder={(holder == null ? "none" : holder.VirtualId.ToString())} " +
            $"stackedWithHolder={(holder != null && holder.Tile == w.Tile)}";
    }

    /// <summary>
    /// Is this unit in the state the harness is waiting for - a JOINER, moving,
    /// with somewhere left to go?
    ///
    /// Joiner means <see cref="PhaseDecision.Aligned"/>: this walk did not
    /// establish the room's phase, it fell in behind one somebody else was
    /// already holding. That is the situation players describe when they report
    /// the hitch, and it is the one worth reproducing.
    ///
    /// A SHADOWED UNIT IS NEVER A CANDIDATE. An escorted suspect goes where
    /// their captor goes and nowhere else - MovementV2Bridge.RequestMove closes
    /// that door for clicks and this closes the same one for the harness.
    ///
    /// Caller MUST hold MovementLock.
    /// </summary>
    private static bool IsCandidate(RoomMovement room, MovementState w, out MovementState? holder)
    {
        holder = null;

        if (w.Mode != MovementMode.Moving)
            return false;
        if (!w.IsRealUser)
            return false;
        if (w.ShadowedBy != MovementState.NoShadow)
            return false;
        if (w.LastPhaseDecision != PhaseDecision.Aligned)
            return false;

        holder = FindHolder(room, w);
        return true;
    }

    /// <summary>
    /// The other real user this one is walking alongside - A, to the harness's
    /// B. Scanned rather than stored, for the same reason MovementController
    /// scans for the phase holder: a counter with a missed decrement is a fault
    /// that only shows up intermittently.
    ///
    /// Caller MUST hold MovementLock.
    /// </summary>
    private static MovementState? FindHolder(RoomMovement room, MovementState self)
    {
        foreach (var other in room.States.Values)
        {
            if (ReferenceEquals(other, self) || !other.IsRealUser)
                continue;
            if (other.Mode == MovementMode.Moving || other.Mode == MovementMode.Pending)
                return other;
        }
        return null;
    }

    /// <summary>
    /// Pick a neighbour of <paramref name="origin"/> that genuinely takes B off
    /// the route it is on, and off A's.
    ///
    /// THREE THINGS DISQUALIFY A TILE. It is where B was going anyway, in which
    /// case the replan restages the same geometry and :movementreplan correctly
    /// reports SAME - a wasted run. It is on A's remaining route, which is not
    /// leaving A at all. Or it is not walkable, which the pathfinder would
    /// reject a moment later anyway.
    ///
    /// Among what survives, the tile facing furthest from B's current heading
    /// wins. The hitch's visible symptom is a sideways jump, so the sharpest
    /// available turn is the one most likely to show it - and the sighting the
    /// investigation already has (8,16-&gt;8,17 becoming 8,16-&gt;7,15) is
    /// exactly that shape.
    ///
    /// Caller MUST hold MovementLock.
    /// </summary>
    private static bool TryPickDivergentTile(
        Gamemap map, MovementState w, MovementState? holder,
        Point origin, Point wouldBe, in TraverseContext ctx,
        out Point firstStep, out Point target)
    {
        firstStep = default;
        target = default;

        var best = -1;
        var bestTurn = -1;

        for (var facing = 0; facing < 8; facing++)
        {
            var d = Delta((byte)facing);
            var candidate = new Point(origin.X + d.X, origin.Y + d.Y);

            if (candidate == origin || candidate == wouldBe)
                continue;
            if (OnRouteOf(holder, candidate))
                continue;

            // Evaluated as a FINAL step, because that is what it is: the forced
            // redirect's destination is one tile away.
            var result = CanTraverse.Evaluate(map, origin, candidate, true, ctx);
            if (!CanTraverse.IsPassable(result, true))
                continue;

            // Facings run 0-7 clockwise, so the turn away from the current
            // heading is the shorter way round the circle - 0 to 4.
            var diff = Math.Abs(facing - w.Facing) % 8;
            var turn = Math.Min(diff, 8 - diff);
            if (turn > bestTurn)
            {
                bestTurn = turn;
                best = facing;
            }
        }

        if (best < 0)
            return false;

        var chosen = Delta((byte)best);
        firstStep = new Point(origin.X + chosen.X, origin.Y + chosen.Y);

        // Push the destination along the same direction for as long as it stays
        // walkable, so the walk survives the turn. Each step is checked from the
        // one before it and as a NON-final step, which is the stricter of the
        // two questions: a tile that is legal only as a route's last tile - a
        // door - stops the extension rather than becoming a destination the
        // pathfinder may then refuse.
        target = firstStep;
        for (var i = 2; i <= ExtendTiles; i++)
        {
            var next = new Point(origin.X + chosen.X * i, origin.Y + chosen.Y * i);
            var step = CanTraverse.Evaluate(map, target, next, false, ctx);
            if (!CanTraverse.IsPassable(step, false))
                break;
            target = next;
        }

        return true;
    }

    /// <summary>
    /// Is this tile on the given walker's remaining route, or under its feet?
    /// Cheap linear scan - a route is a couple of dozen tiles at most and this
    /// runs once per attempt, not per beat.
    ///
    /// Caller MUST hold MovementLock.
    /// </summary>
    private static bool OnRouteOf(MovementState? other, Point tile)
    {
        if (other == null)
            return false;
        if (other.Tile == tile || other.EdgeTo == tile)
            return true;

        for (var i = other.Route.Cursor; i < other.Route.Length; i++)
        {
            if (other.Route[i] == tile)
                return true;
        }
        return false;
    }

    /// <summary>
    /// The eight facings as tile deltas. A local copy of MovementController's
    /// table rather than a widening of its visibility: a diagnostic should not
    /// change the surface of the thing it is diagnosing, and eight lines is a
    /// cheaper price than an internal that outlives the investigation.
    /// </summary>
    private static Point Delta(byte facing) => facing switch
    {
        0 => new Point(0, -1),
        1 => new Point(1, -1),
        2 => new Point(1, 0),
        3 => new Point(1, 1),
        4 => new Point(0, 1),
        5 => new Point(-1, 1),
        6 => new Point(-1, 0),
        7 => new Point(-1, -1),
        _ => new Point(0, 0),
    };
}

namespace Plus.HabboHotel.Rooms.Movement;

/// <summary>
/// Health counters. Cheap interlocked increments only - no string formatting and
/// no console I/O on the movement path, which is what made V1's telemetry a
/// measurable cost on the tick (audit D3-D6).
/// </summary>
public static class MovementCounters
{
    private static long _orphansRecovered;
    private static long _drainDeferred;
    private static long _beatsLate;
    private static long _maxBeatLatenessMs;
    private static long _roomFaults;
    private static long _pathfindCalls;
    private static long _pathfindPartial;
    private static long _pathfindFailed;

    // Stage counters. Added after two beta freezes that could not be located
    // from the outside: every aggregate looked healthy (no faults, no lateness,
    // no orphans, frames flowing) while avatars still stopped. These pin down
    // WHICH stage stops rather than proving the system is "fine" in aggregate.
    private static long _walkStarts;
    private static long _redirects;
    private static long _advances;
    private static long _commits;
    private static long _stopsRouteEnd;
    private static long _stopsBlocked;
    private static long _replans;
    private static long _replansDeferred;
    private static long _roomProcessed;
    private static long _drainedWalkers;

    // The scheduler thread's own faults, as distinct from a room's. Before the
    // per-iteration isolation these did not exist as a category: an exception
    // outside the per-room try simply ended the thread, so the counter that
    // would have named the freeze was never incremented and roomFaults - the
    // only fault counter there was - stayed convincingly at zero.
    private static long _schedulerFaults;
    private static string _lastSchedulerFault = "none";
    private static long _lastSchedulerFaultAtMs;

    public static long SchedulerFaults => Interlocked.Read(ref _schedulerFaults);
    public static string LastSchedulerFault => Volatile.Read(ref _lastSchedulerFault);
    public static long LastSchedulerFaultAtMs => Interlocked.Read(ref _lastSchedulerFaultAtMs);

    /// <summary>
    /// Record a fault that escaped one scheduler beat. Keeps the type and the
    /// first frame only: this is read over a whisper, not a log file.
    /// </summary>
    public static void SchedulerFault(Exception e)
    {
        Interlocked.Increment(ref _schedulerFaults);
        Interlocked.Exchange(ref _lastSchedulerFaultAtMs, SystemMovementClock.Instance.NowMs);
        try
        {
            var trace = e.StackTrace ?? string.Empty;
            var cut = trace.IndexOf('\n');
            var frame = (cut >= 0 ? trace.Substring(0, cut) : trace).Trim();
            if (frame.Length > 160)
                frame = frame.Substring(0, 160);
            Volatile.Write(ref _lastSchedulerFault, $"{e.GetType().Name}: {e.Message} @ {frame}");
        }
        catch
        {
            Volatile.Write(ref _lastSchedulerFault, e.GetType().Name);
        }
        Plus.Core.ExceptionLogger.LogCriticalException(e);
    }

    // The busy-spin detector. A room popped as due must advance one of the three
    // terms ComputeNextDue takes a minimum of; if it advances none and still
    // wants an expired tick, it will be popped again at once and the single
    // scheduler thread does nothing else for as long as that lasts. That is not
    // a slow room, it is a wedged hotel, and it needs a name of its own.
    private static long _spinGuards;
    private static long _lastSpinRoomId;

    public static long SpinGuards => Interlocked.Read(ref _spinGuards);
    public static long LastSpinRoomId => Interlocked.Read(ref _lastSpinRoomId);

    public static void SpinGuard(uint roomId)
    {
        Interlocked.Increment(ref _spinGuards);
        Interlocked.Exchange(ref _lastSpinRoomId, roomId);
    }

    // ---- ACTIVE-EDGE REWRITE EXPOSURE (measurement only) ------------------
    // A redirect stages from e + 1, and at the instant it is planned that is
    // correct: edge e + 1 has not started. But the CLIENT begins an edge from
    // LOOKAHEAD the moment its cycleStart passes - before this server has
    // emitted any record for that index - and the correction is still in
    // flight then. If the boundary is nearer than the flight time, the client
    // has already begun the very edge being restaged, and its geometry changes
    // under a live phase. That is the crossing/following hitch, measured on
    // beta as edge 103 turning from 8,16->8,17 into 8,16->7,15 at phase 0.128.
    //
    // These counters exist to size that exposure BEFORE anything is changed to
    // avoid it: how near the boundary redirects actually land, and how often.
    // Nothing here alters behaviour - they are interlocked increments on the
    // click path, not the tick.
    private static long _redirectMarginUnder50;
    private static long _redirectMarginUnder100;
    private static long _redirectMarginUnder250;
    private static long _minRedirectMarginMs = long.MaxValue;

    // Early publication of a corrected e+1 (experiment). Cheap interlocked
    // increments only - no strings and no I/O on the movement path.
    private static long _correctionEPlus1ImmediateStaged;
    private static long _correctionEPlus1NotFuture;
    private static long _correctionEPlus1AlreadyStaged;
    private static long _correctionEPlus1Escort;

    // Redirects held back because the walker was behind the elapsing index,
    // and the ones that were later applied successfully.
    private static long _redirectDeferredBehindElapsing;
    private static long _redirectDeferredRecovered;

    /// <summary>
    /// Milliseconds from this redirect to the start of the edge it restages.
    /// Small values are the exposure: the smaller it is, the more certain that
    /// the client has already begun that edge when the correction arrives.
    /// </summary>
    public static void RedirectMargin(long marginMs)
    {
        if (marginMs < 250)
            Interlocked.Increment(ref _redirectMarginUnder250);
        if (marginMs < 100)
            Interlocked.Increment(ref _redirectMarginUnder100);
        if (marginMs < 50)
            Interlocked.Increment(ref _redirectMarginUnder50);

        long observed;
        while (marginMs < (observed = Interlocked.Read(ref _minRedirectMarginMs)))
        {
            if (Interlocked.CompareExchange(ref _minRedirectMarginMs, marginMs, observed) == observed)
                break;
        }
    }

    /// <summary>A corrected e+1 was transmitted as soon as the redirect decided it.</summary>
    public static void CorrectionEPlus1ImmediateStaged() =>
        Interlocked.Increment(ref _correctionEPlus1ImmediateStaged);

    /// <summary>
    /// The pathfind crossed a boundary: by the time the correction was staged the
    /// index was no longer future, so it was left to the normal pipeline. This is
    /// the counter that makes the freshness check meaningful rather than vacuous.
    /// </summary>
    public static void CorrectionEPlus1NotFuture() =>
        Interlocked.Increment(ref _correctionEPlus1NotFuture);

    /// <summary>Same (session, revision, index) had already been published early.</summary>
    public static void CorrectionEPlus1AlreadyStaged() =>
        Interlocked.Increment(ref _correctionEPlus1AlreadyStaged);

    /// <summary>
    /// Skipped because the walker is escorting. A captor's edges ride with a
    /// matching shadow record staged by StageShadow; publishing the captor's
    /// e+1 alone would break that lockstep, so escorts keep the normal path.
    /// </summary>
    public static void CorrectionEPlus1Escort() =>
        Interlocked.Increment(ref _correctionEPlus1Escort);

    /// <summary>
    /// A redirect was NOT planned because EdgeIndex was behind the elapsing
    /// index. The target was kept for a later beat. Pairs with
    /// redirectBehindElapsing, which counts the same condition being reached;
    /// with the deferral in place that counter should now stay flat.
    /// </summary>
    public static void RedirectDeferredBehindElapsing() =>
        Interlocked.Increment(ref _redirectDeferredBehindElapsing);

    /// <summary>A deferred redirect was retried on a later beat and applied.</summary>
    public static void RedirectDeferredRecovered() =>
        Interlocked.Increment(ref _redirectDeferredRecovered);

    private static string MinRedirectMargin()
    {
        var value = Interlocked.Read(ref _minRedirectMarginMs);
        return value == long.MaxValue ? "-" : value.ToString();
    }

    public static void WalkStart() => Interlocked.Increment(ref _walkStarts);
    public static void Redirect() => Interlocked.Increment(ref _redirects);
    public static void Advance() => Interlocked.Increment(ref _advances);
    public static void Commit() => Interlocked.Increment(ref _commits);
    public static void StopRouteEnd() => Interlocked.Increment(ref _stopsRouteEnd);
    public static void StopBlocked() => Interlocked.Increment(ref _stopsBlocked);
    public static void Replan() => Interlocked.Increment(ref _replans);

    /// <summary>
    /// A blocked-at-commit re-plan that was DEFERRED because the edge it would
    /// have restaged had already begun. Once an edge has started its geometry
    /// is immutable, so the re-plan waits one beat rather than changing the
    /// route under an avatar that is already walking it.
    /// </summary>
    public static void ReplanDeferred() => Interlocked.Increment(ref _replansDeferred);
    public static void RoomProcessed() => Interlocked.Increment(ref _roomProcessed);
    public static void DrainedWalker() => Interlocked.Increment(ref _drainedWalkers);

    public static string StageSnapshot() =>
        $"starts={Interlocked.Read(ref _walkStarts)} " +
        $"redirects={Interlocked.Read(ref _redirects)} " +
        $"roomProcessed={Interlocked.Read(ref _roomProcessed)} " +
        $"drained={Interlocked.Read(ref _drainedWalkers)} " +
        $"advances={Interlocked.Read(ref _advances)} " +
        $"commits={Interlocked.Read(ref _commits)} " +
        $"replans={Interlocked.Read(ref _replans)} " +
        $"replansDeferred={Interlocked.Read(ref _replansDeferred)} " +
        $"stopEnd={Interlocked.Read(ref _stopsRouteEnd)} " +
        $"stopBlocked={Interlocked.Read(ref _stopsBlocked)} " +
        $"redirectMarginUnder250={Interlocked.Read(ref _redirectMarginUnder250)} " +
        $"under100={Interlocked.Read(ref _redirectMarginUnder100)} " +
        $"under50={Interlocked.Read(ref _redirectMarginUnder50)} " +
        $"minRedirectMarginMs={MinRedirectMargin()} " +
        $"correctionEPlus1ImmediateStaged={Interlocked.Read(ref _correctionEPlus1ImmediateStaged)} " +
        $"correctionEPlus1NotFuture={Interlocked.Read(ref _correctionEPlus1NotFuture)} " +
        $"correctionEPlus1AlreadyStaged={Interlocked.Read(ref _correctionEPlus1AlreadyStaged)} " +
        $"correctionEPlus1Escort={Interlocked.Read(ref _correctionEPlus1Escort)} " +
        $"redirectDeferredBehindElapsing={Interlocked.Read(ref _redirectDeferredBehindElapsing)} " +
        $"redirectDeferredRecovered={Interlocked.Read(ref _redirectDeferredRecovered)}";

    public static void OrphanRecovered() => Interlocked.Increment(ref _orphansRecovered);
    public static void DrainDeferred() => Interlocked.Increment(ref _drainDeferred);
    public static void RoomFault() => Interlocked.Increment(ref _roomFaults);
    public static void PathfindCall() => Interlocked.Increment(ref _pathfindCalls);
    public static void PathfindPartial() => Interlocked.Increment(ref _pathfindPartial);
    public static void PathfindFailed() => Interlocked.Increment(ref _pathfindFailed);

    public static void BeatLate(long lateMs)
    {
        Interlocked.Increment(ref _beatsLate);
        long observed;
        while (lateMs > (observed = Interlocked.Read(ref _maxBeatLatenessMs)))
        {
            if (Interlocked.CompareExchange(ref _maxBeatLatenessMs, lateMs, observed) == observed)
                break;
        }
    }

    public static string Snapshot() =>
        $"[MOVEMENT_V2_COUNTERS] orphansRecovered={Interlocked.Read(ref _orphansRecovered)} " +
        $"drainDeferred={Interlocked.Read(ref _drainDeferred)} " +
        $"beatsLate={Interlocked.Read(ref _beatsLate)} " +
        $"maxBeatLatenessMs={Interlocked.Read(ref _maxBeatLatenessMs)} " +
        $"roomFaults={Interlocked.Read(ref _roomFaults)} " +
        $"schedulerFaults={Interlocked.Read(ref _schedulerFaults)} " +
        $"spinGuards={Interlocked.Read(ref _spinGuards)} " +
        $"lastSpinRoom={Interlocked.Read(ref _lastSpinRoomId)} " +
        $"pathfind={Interlocked.Read(ref _pathfindCalls)} " +
        $"partial={Interlocked.Read(ref _pathfindPartial)} " +
        $"failed={Interlocked.Read(ref _pathfindFailed)}";
}

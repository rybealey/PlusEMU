using System.Collections.Concurrent;
using Plus.Core;

namespace Plus.HabboHotel.Rooms.Movement;

/// <summary>
/// pixelrp Movement V2: the boundary between the existing hotel and V2.
///
/// This is the ONLY place the rest of the emulator touches V2. V2 is always on -
/// there is no runtime kill switch - so rolling back means reverting the commit
/// and deploying, which leaves the hotel in one unambiguous state rather than a
/// mixed one.
///
/// A7 LIFECYCLE ORDER (mandatory, and the reason a hotel-wide scheduler is safe):
///   1. acquire MovementLock
///   2. mark the room Closed
///   3. remove it from the scheduler's room heap
///   4. clear scheduled movement work
///   5. release the lock
///   6. ONLY THEN dispose Gamemap / room resources
/// Room.Dispose disposes the Gamemap (nulling _userMap, GameMap, Model and the
/// rest), so a scheduler still holding the room would dereference null on a
/// single thread and freeze movement hotel-wide.
/// </summary>
public static class MovementRegistry
{
    private static readonly ConcurrentDictionary<uint, RoomMovement> Rooms = new();
    private static int _started;

    /// <summary>Starts the scheduler thread and the Q1/Q2 workers. Idempotent.</summary>
    public static void EnsureStarted()
    {
        if (Interlocked.CompareExchange(ref _started, 1, 0) != 0)
            return;
        MovementWorkQueues.Start();
        MovementScheduler.Instance.Start();
    }

    public static void Shutdown()
    {
        if (Interlocked.CompareExchange(ref _started, 0, 1) != 1)
            return;
        MovementScheduler.Instance.Stop();
        MovementWorkQueues.Stop();
        foreach (var roomId in Rooms.Keys)
            Rooms.TryRemove(roomId, out _);
    }

    public static bool TryGet(uint roomId, out RoomMovement? room) => Rooms.TryGetValue(roomId, out room);

    /// <summary>
    /// Attach V2 movement to a room. Called lazily on first user entry, so a
    /// room with nobody in it never enters the scheduler.
    /// </summary>
    public static RoomMovement? Attach(Room room)
    {
        if (room == null)
            return null;
        EnsureStarted();

        var movement = Rooms.GetOrAdd(room.RoomId, _ => new RoomMovement(room));
        var now = MovementScheduler.Instance.Clock.NowMs;
        lock (movement.MovementLock)
        {
            if (movement.Closed)
                return null;
            movement.NextFlushTick = now + MovementSettings.FlushIntervalMs;
            movement.NextWatchdogTick = now + MovementSettings.WatchdogIntervalMs;
        }
        MovementScheduler.Instance.RegisterRoom(movement);
        return movement;
    }

    /// <summary>
    /// A7 teardown. Safe to call unconditionally, including for rooms V2 never
    /// attached to, so Room.Dispose can call it without checking anything.
    /// </summary>
    public static void Detach(uint roomId)
    {
        if (!Rooms.TryRemove(roomId, out var movement))
            return;

        try
        {
            lock (movement.MovementLock)
                movement.Close(); // steps 1, 2 and 4

            MovementScheduler.Instance.UnregisterRoom(movement); // step 3
            MovementWorkQueues.ForgetRoom(roomId);
        }
        catch (Exception e)
        {
            ExceptionLogger.LogException(e);
        }
    }

    /// <summary>Snapshot of a room's units. Caller must hold MovementLock.</summary>
    public static IEnumerable<MovementState> WalkersOf(RoomMovement room) => room.States.Values;

    /// <summary>Enrol a unit. Caller must hold MovementLock.</summary>
    public static MovementState GetOrCreateState(RoomMovement room, int virtualId)
    {
        if (room.States.TryGetValue(virtualId, out var existing))
            return existing;
        var state = new MovementState { VirtualId = virtualId };
        room.States[virtualId] = state;
        return state;
    }

    /// <summary>Remove a unit. Caller must hold MovementLock.</summary>
    public static void RemoveState(RoomMovement room, int virtualId)
    {
        if (!room.States.TryGetValue(virtualId, out var state))
            return;
        // Dequeue BEFORE removal, so nothing can be emitted for a unit that is
        // already gone.
        room.Walkers.Remove(state);
        state.Queued = false;
        room.States.Remove(virtualId);
    }

    public static string Snapshot() =>
        $"[MOVEMENT_V2] rooms={Rooms.Count} " +
        $"schedulerRunning={MovementScheduler.Instance.IsRunning} " +
        $"framesHandedOff={MovementWorkQueues.FramesHandedOff} " +
        $"{MovementCounters.StageSnapshot()} {MovementCounters.Snapshot()}";

    // ---- live diagnostics -------------------------------------------------
    // The emulator log is not reachable from the dev machine, so these exist to
    // put the answer where it CAN be read: in a whisper, in game, at the moment
    // an avatar is frozen. Read-only; nothing here mutates movement state.

    /// <summary>
    /// The three threads movement depends on, and whether each is alive and
    /// beating. This is the first line to read during a freeze: it separates
    /// "the beat stopped" from "the beat is fine and the wire stopped".
    /// </summary>
    public static string Health()
    {
        var scheduler = MovementScheduler.Instance;
        var closed = 0;
        foreach (var room in Rooms.Values)
        {
            if (room.Closed)
                closed++;
        }

        return "[MV2/health] " +
               $"sched(alive={scheduler.IsRunning} loopAge={scheduler.LoopAgeMs}ms " +
               $"faults={MovementCounters.SchedulerFaults}) " +
               $"queues(alive={MovementWorkQueues.WorkersAlive} " +
               $"q1Age={MovementWorkQueues.OutboundAgeMs}ms q1Depth={MovementWorkQueues.OutboundDepth} " +
               $"q2Age={MovementWorkQueues.EventAgeMs}ms " +
               $"frames={MovementWorkQueues.FramesHandedOff}) " +
               $"rooms={Rooms.Count} closedRooms={closed}";
    }

    /// <summary>Type and site of the last fault that escaped a scheduler beat.</summary>
    public static string LastFault() =>
        $"[MV2/lastFault] {MovementCounters.LastSchedulerFault}";

    /// <summary>
    /// One room's scheduling state plus every unit V2 knows about in it.
    ///
    /// dueIn is the decisive per-walker number: a Moving walker whose dueIn has
    /// gone far negative is one the scheduler has stopped draining, and
    /// queued=False on a Moving walker is the orphan the watchdog exists to fix.
    /// </summary>
    public static List<string> DescribeRoom(uint roomId)
    {
        var lines = new List<string>();
        if (!Rooms.TryGetValue(roomId, out var room) || room == null)
        {
            lines.Add($"[MV2/room {roomId}] NOT ATTACHED - no movement state for this room.");
            return lines;
        }

        var now = MovementScheduler.Instance.Clock.NowMs;

        if (room.Closed)
        {
            // A closed room is permanently dead: Attach returns null, Owns is
            // false, and every click in it is silently dropped.
            lines.Add($"[MV2/room {roomId}] CLOSED - this room will never move again until it reloads.");
            return lines;
        }

        lock (room.MovementLock)
        {
            var interval = MovementSettings.IntervalMs;
            var holders = 0;

            // inPhase IS THE SUCCESS CONDITION, evaluated directly: do all the
            // real users currently holding the phase share one cycleStart % 500?
            // Reading it off two unit lines by eye is exactly the kind of manual
            // comparison that hides an intermittent failure.
            var inPhase = true;
            var observed = -1L;
            foreach (var unit in room.States.Values)
            {
                if (!unit.IsRealUser ||
                    (unit.Mode != MovementMode.Moving && unit.Mode != MovementMode.Pending))
                    continue;
                holders++;
                var unitPhase = ((unit.TimelineOrigin % interval) + interval) % interval;
                if (observed < 0)
                    observed = unitPhase;
                else if (observed != unitPhase)
                    inPhase = false;
            }

            // phase is the shared remainder every aligned walker's cycleStart
            // must match; holders is what keeps it alive. holders=0 means the
            // next real walker establishes a fresh one.
            lines.Add($"[MV2/phase {roomId}] anchor={room.PhaseAnchor} " +
                      $"phase={((room.PhaseAnchor % interval) + interval) % interval} " +
                      $"holders={holders} inPhase={(holders > 1 ? inPhase.ToString() : "n/a")} " +
                      $"maxStartDelay={MovementSettings.MaxStartDelayMs}ms");

            lines.Add($"[MV2/room {roomId}] units={room.States.Count} queued={room.Walkers.Count} " +
                      $"staged={room.Staged.Count} hasStaged={room.HasStagedWork} hasImmediate={room.HasImmediateWork} " +
                      $"nextDueIn={room.ComputeNextDue() - now}ms snapshotIn={room.NextDueSnapshot - now}ms " +
                      $"flushIn={(room.HasStagedWork ? (room.NextFlushTick - now) + "ms" : "idle")} " +
                      $"watchdogIn={room.NextWatchdogTick - now}ms " +
                      $"frame={room.FrameSequence}");

            foreach (var walker in room.States.Values)
            {
                lines.Add($"[MV2/unit {walker.VirtualId}] mode={walker.Mode} " +
                          $"session={walker.WalkSessionId} rev={walker.RouteRevision} edge={walker.EdgeIndex} " +
                          $"emittedThrough={walker.EmittedThroughEdge} " +
                          $"queued={walker.Queued} inHeap={room.Walkers.Contains(walker)} " +
                          $"dueIn={(walker.Queued ? walker.DueTick - now : 0)}ms " +
                          $"elapsing={(walker.Mode == MovementMode.Moving || walker.Mode == MovementMode.Pending ? walker.ElapsingEdgeIndex(now).ToString() : "-")} " +
                          $"real={walker.IsRealUser} align={walker.LastPhaseDecision} " +
                          $"startDelay={walker.LastStartDelayMs}ms " +
                          $"cycleStartPhase={((walker.TimelineOrigin % MovementSettings.IntervalMs) + MovementSettings.IntervalMs) % MovementSettings.IntervalMs} " +
                          $"tile={walker.Tile.X},{walker.Tile.Y} -> {walker.EdgeTo.X},{walker.EdgeTo.Y} " +
                          $"target={walker.Target.X},{walker.Target.Y} routeLeft={walker.Route.Length - walker.Route.Cursor}");
            }
        }

        return lines;
    }
}

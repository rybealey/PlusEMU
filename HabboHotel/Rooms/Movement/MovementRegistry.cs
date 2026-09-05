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
        $"framesHandedOff={MovementWorkQueues.FramesHandedOff} {MovementCounters.Snapshot()}";
}

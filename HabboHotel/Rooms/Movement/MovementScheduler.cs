using System.Collections.Concurrent;
using System.Diagnostics;
using Plus.Core;

namespace Plus.HabboHotel.Rooms.Movement;

/// <summary>
/// pixelrp Movement V2 (A6): the ONE movement scheduler for the whole hotel.
///
/// Replaces V1's two independent clocks - the room tick (RoomManager.OnCycle's
/// ">= 500ms" sleep-poll) and one fire-and-forget SelfPaceWalk task per walker.
/// A single dedicated thread owns all movement ordering, so there is no
/// ThreadPool dependency on the movement path at all (V1's per-walker
/// continuations are exactly what starved on the 2-core VPS).
///
/// THIS THREAD MAY ONLY: mutate MovementState, commit due edges, plan routes,
/// stage frames, and manage its heaps and queues. It may NOT touch the database,
/// a socket, a wired/furni callback, disconnect logic, or any blocking sink -
/// see MovementSchedulerGuard, which asserts this in DEBUG.
///
/// V2 is always on; there is no runtime kill switch to reason about.
/// </summary>
public sealed class MovementScheduler
{
    public static readonly MovementScheduler Instance = new();

    /// <summary>
    /// How long a barrier-blocked walker is deferred before it is reconsidered.
    /// Large enough that a blocked walker cannot spin the scheduler, small
    /// enough to be invisible against a 500ms edge.
    /// </summary>
    private const int BarrierRetryMs = 25;

    private readonly IndexedDueHeap<RoomMovement> _rooms = new(64);
    private readonly ConcurrentQueue<RoomMovement> _signalled = new();
    private readonly ManualResetEventSlim _wake = new(false);
    private readonly object _roomsLock = new();

    private Thread? _thread;
    private volatile bool _running;

    public IMovementClock Clock { get; private set; } = SystemMovementClock.Instance;

    private MovementScheduler() { }

    /// <summary>Test seam: swap in a ManualMovementClock before Start().</summary>
    public void UseClock(IMovementClock clock) => Clock = clock;

    public bool IsRunning => _running;

    public void Start()
    {
        if (_running)
            return;
        _running = true;
        _thread = new Thread(Loop)
        {
            IsBackground = true,
            Name = "PixelRPMovement",
            // Above normal for the same reason the game cycle is: the movement
            // beat must win over pooled work when both cores are busy.
            Priority = ThreadPriority.AboveNormal
        };
        _thread.Start();
    }

    public void Stop()
    {
        _running = false;
        _wake.Set();
        _thread?.Join(2000);
        _thread = null;
        MovementSchedulerGuard.ClearSchedulerThread();
    }

    // ---- room registration ------------------------------------------------

    public void RegisterRoom(RoomMovement room)
    {
        lock (_roomsLock)
        {
            if (room.Closed)
                return;
            var due = room.ComputeNextDue();
            if (due != long.MaxValue)
                _rooms.InsertOrUpdate(room, due);
        }
        _wake.Set();
    }

    /// <summary>
    /// Remove a room permanently (A7). Called during teardown AFTER the room's
    /// movement state has been closed under its own lock, and BEFORE the
    /// Gamemap is disposed.
    /// </summary>
    public void UnregisterRoom(RoomMovement room)
    {
        lock (_roomsLock)
            _rooms.Remove(room);
    }

    /// <summary>
    /// Ask the scheduler to look at this room immediately. Called from packet
    /// threads after staging a latency-critical edge, and from Q2 workers after
    /// releasing a movement barrier.
    ///
    /// Cheap and idempotent: a second signal before the scheduler wakes is
    /// coalesced, so 20 spam-clicks cannot produce 20 wake-ups.
    /// </summary>
    public void Signal(RoomMovement room)
    {
        if (room.Closed)
            return;
        if (room.TrySetSignalPending())
            _signalled.Enqueue(room);
        _wake.Set();
    }

    // ---- main loop --------------------------------------------------------

    private void Loop()
    {
        MovementSchedulerGuard.MarkCurrentThreadAsScheduler();
        try
        {
            while (_running)
            {
                var now = Clock.NowMs;
                long nextDue;
                lock (_roomsLock)
                    nextDue = _rooms.PeekDue;

                var waitMs = nextDue == long.MaxValue
                    ? MovementSettings.MaxSleepMs
                    : (int)Math.Clamp(nextDue - now, MovementSettings.MinSleepMs, MovementSettings.MaxSleepMs);

                // A Signal cuts this short; MaxSleepMs is only the idle ceiling.
                _wake.Wait(waitMs);
                _wake.Reset();
                if (!_running)
                    break;

                now = Clock.NowMs;

                // 1. SIGNALLED ROOMS FIRST - this is the latency path.
                while (_signalled.TryDequeue(out var signalledRoom))
                {
                    signalledRoom.ClearSignalPending();
                    ProcessRoomIsolated(signalledRoom, now);
                }

                // 2. TIME-DRIVEN ROOMS.
                while (true)
                {
                    RoomMovement? room;
                    lock (_roomsLock)
                    {
                        if (_rooms.Count == 0 || _rooms.PeekDue > now + MovementSettings.TickSlackMs)
                            break;
                        room = _rooms.Pop();
                    }
                    if (room == null)
                        break;
                    ProcessRoomIsolated(room, now);
                }
            }
        }
        catch (Exception e)
        {
            ExceptionLogger.LogCriticalException(e);
        }
        finally
        {
            MovementSchedulerGuard.ClearSchedulerThread();
        }
    }

    /// <summary>
    /// Per-room exception isolation (A7). One bad or disposed room must never
    /// terminate the hotel-wide thread: it is closed, unregistered and logged,
    /// and every other room keeps beating.
    /// </summary>
    private void ProcessRoomIsolated(RoomMovement room, long now)
    {
        if (room.Closed)
        {
            lock (_roomsLock)
                _rooms.Remove(room);
            return;
        }

        try
        {
            ProcessRoom(room, now);
        }
        catch (Exception e)
        {
            MovementCounters.RoomFault();
            try
            {
                lock (room.MovementLock)
                    room.Close();
            }
            catch { /* teardown must not throw twice */ }
            lock (_roomsLock)
                _rooms.Remove(room);
            ExceptionLogger.LogException(e);
            return;
        }

        lock (_roomsLock)
        {
            if (room.Closed)
            {
                _rooms.Remove(room);
            }
            else
            {
                var due = room.ComputeNextDue();
                if (due == long.MaxValue)
                    _rooms.Remove(room);
                else
                    _rooms.InsertOrUpdate(room, due); // never a bare Push
            }
        }
    }

    private void ProcessRoom(RoomMovement room, long now)
    {
        var started = Stopwatch.GetTimestamp();
        var budgetTicks = (long)(MovementSettings.DrainBudgetUs * (Stopwatch.Frequency / 1_000_000.0));

        lock (room.MovementLock)
        {
            if (room.Closed)
                return;

            // A. DRAIN DUE COMMITS.
            var drained = 0;
            while (room.Walkers.Count > 0
                   && room.Walkers.PeekDue <= now + MovementSettings.TickSlackMs
                   && drained < MovementSettings.MaxDrainPerRoom)
            {
                if (drained > 0 && (Stopwatch.GetTimestamp() - started) > budgetTicks)
                {
                    // Fuse: yield to other rooms rather than monopolise the
                    // single thread. The remainder resumes on the next pass.
                    MovementCounters.DrainDeferred();
                    break;
                }

                var walker = room.Walkers.Peek();
                if (walker == null)
                    break;

                // The movement-critical tile barrier (A9): do not commit past an
                // edge whose tile events are still being processed by Q2.
                //
                // MUST NOT simply `break` while leaving this walker at a due
                // tick in the past: ComputeNextDue would return that past tick,
                // the room would be instantly due again, and the scheduler
                // would spin hot on a single thread - which starves the whole
                // hotel, not just this room. Defer the blocked walker a little
                // and keep draining the others instead.
                if (walker.BarrierBlocks(walker.EdgeIndex + 1))
                {
                    MovementCounters.BarrierWait();
                    room.Walkers.InsertOrUpdate(walker, now + BarrierRetryMs);
                    continue;
                }

                room.Walkers.Remove(walker);
                walker.Queued = false;
                drained++;

                MovementController.AdvanceWalker(room, walker, walker.DueTick, now);
            }

            // B. WATCHDOG - the direct fix for V1's unrecoverable frozen avatar.
            if (now >= room.NextWatchdogTick)
            {
                room.NextWatchdogTick = now + MovementSettings.WatchdogIntervalMs;
                MovementController.RecoverOrphans(room, now);
            }

            // C. SEAL. Staging -> one frame; handed to Q1, never sent here.
            if (room.HasImmediateWork || (now >= room.NextFlushTick && room.HasStagedWork))
            {
                room.FrameSequence++;
                room.HasStagedWork = false;
                room.HasImmediateWork = false;
                room.NextFlushTick = now + MovementSettings.FlushIntervalMs;

                // Hand the frame off by VALUE and clear staging, so the worker
                // can apply it without holding the movement lock and the
                // scheduler can keep beating meanwhile.
                if (room.Staged.Count > 0)
                {
                    var frame = room.Staged.ToArray();
                    room.Staged.Clear();
                    MovementWorkQueues.EnqueueOutbound(room, frame);
                }
            }
        }
    }
}

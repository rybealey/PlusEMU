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

    /// <summary>Movement-clock tick of the last completed loop iteration.</summary>
    private long _lastLoopMs;

    public IMovementClock Clock { get; private set; } = SystemMovementClock.Instance;

    private MovementScheduler() { }

    /// <summary>Test seam: swap in a ManualMovementClock before Start().</summary>
    public void UseClock(IMovementClock clock) => Clock = clock;

    /// <summary>
    /// TRUE ONLY IF THE THREAD IS ACTUALLY ALIVE.
    ///
    /// This used to return the _running flag alone, which is set once at Start()
    /// and never cleared when the loop dies. A scheduler that had thrown its way
    /// out of Loop() therefore still reported "running" while no avatar in the
    /// hotel could move - the exact reading that made the beta freeze look like
    /// a client bug. Liveness is now the thread's, not a flag's.
    /// </summary>
    public bool IsRunning => _running && _thread is { IsAlive: true };

    /// <summary>Age in ms of the last completed loop iteration. Huge = wedged.</summary>
    public long LoopAgeMs => Clock.NowMs - Volatile.Read(ref _lastLoopMs);

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
        // ComputeNextDue reads the walker heap, which is owned by MovementLock.
        // Take that lock FIRST and separately: holding _roomsLock while waiting
        // for a packet thread's A* to finish would serialise every other room
        // behind it. Order is _roomsLock -> MovementLock nowhere in this class.
        long due;
        lock (room.MovementLock)
        {
            if (room.Closed)
                return;
            due = room.RefreshNextDue();
        }

        lock (_roomsLock)
        {
            if (room.Closed)
                return;
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

    /// <summary>
    /// THE HOTEL-WIDE MOVEMENT BEAT. It must be impossible for this thread to
    /// exit while _running is set.
    ///
    /// It previously had ONE try/catch wrapped around the whole `while`, so a
    /// single escaped exception - from anywhere outside the per-room try - ended
    /// the thread permanently. There is no supervisor and no restart path, so
    /// that is a hotel-wide, unrecoverable movement death: every avatar in every
    /// room freezes exactly where it stands, further clicks stage edges that are
    /// never sealed, roomFaults stays 0 because the fault was never per-room,
    /// and IsRunning still answered "true". That is the beta freeze.
    ///
    /// The isolation is now PER ITERATION. A fault costs one beat and is counted;
    /// it can never cost the hotel its movement thread.
    /// </summary>
    private void Loop()
    {
        MovementSchedulerGuard.MarkCurrentThreadAsScheduler();
        try
        {
            while (_running)
            {
                try
                {
                    LoopOnce();
                }
                catch (Exception e)
                {
                    // Never rethrow: surviving is the entire point.
                    MovementCounters.SchedulerFault(e);
                    // A pathological throw before the wait would otherwise spin
                    // this AboveNormal thread against both cores.
                    Thread.Sleep(1);
                }
            }
        }
        finally
        {
            MovementSchedulerGuard.ClearSchedulerThread();
        }
    }

    /// <summary>One beat. Anything it throws costs this beat and nothing more.</summary>
    private void LoopOnce()
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
            return;

        now = Clock.NowMs;

        // 1. SIGNALLED ROOMS FIRST - this is the latency path.
        // Re-sampled for the same reason as the time-driven loop below: a room
        // may be re-signalled by a Q2 worker while this drains, so the clock a
        // room is processed against must be the current one, not the one this
        // pass started with.
        while (_signalled.TryDequeue(out var signalledRoom))
        {
            signalledRoom.ClearSignalPending();
            ProcessRoomIsolated(signalledRoom, Clock.NowMs);
        }

        // 2. TIME-DRIVEN ROOMS.
        //
        // `now` IS RE-SAMPLED EVERY ITERATION, and that is not a refinement.
        // This loop can pop the SAME room again the moment ProcessRoomIsolated
        // re-queues it, so a `now` captured once outside the loop is a clock
        // that never advances no matter how long the loop runs - and a room
        // whose next due tick sits 1-2ms ahead is then permanently "due enough
        // to pop, not due enough to work". That wedged the whole hotel: one
        // room spun 92 million passes in six seconds while every other room's
        // walkers sat overdue and undrained, because the single scheduler
        // thread never got back to the top of LoopOnce.
        var processed = 0;
        while (processed < MovementSettings.MaxRoomsPerPass)
        {
            now = Clock.NowMs;

            RoomMovement? room;
            lock (_roomsLock)
            {
                if (_rooms.Count == 0 || _rooms.PeekDue > now + MovementSettings.TickSlackMs)
                    break;
                room = _rooms.Pop();
            }
            if (room == null)
                break;
            processed++;
            ProcessRoomIsolated(room, now);
        }

        // The heartbeat the diagnostics read. A stalled value means the beat is
        // wedged on something; a fresh one with frozen avatars means the fault
        // is downstream of the scheduler.
        Volatile.Write(ref _lastLoopMs, Clock.NowMs);
    }

    /// <summary>
    /// Per-room exception isolation (A7). One bad or disposed room must never
    /// terminate the hotel-wide thread, and every other room keeps beating.
    ///
    /// A FAULT IS NOT AUTOMATICALLY A DEATH SENTENCE. This used to Close() the
    /// room on the first exception of any kind, which is right for a room whose
    /// Gamemap has been disposed and catastrophic for one that merely took a
    /// transient throw: a closed room is permanent (Attach returns null, Owns is
    /// false, RequestMove drops every click), so a single unlucky beat froze
    /// every avatar in that room until it reloaded.
    ///
    /// The room is retired ONLY when it is genuinely unusable - its map is gone.
    /// Otherwise the fault costs one beat, and a walker left out of the heap by
    /// the throw is re-queued by the orphan watchdog within a second, which is
    /// exactly the case I-12 put it there for.
    /// </summary>
    private void ProcessRoomIsolated(RoomMovement room, long now)
    {
        if (room.Closed)
        {
            lock (_roomsLock)
                _rooms.Remove(room);
            return;
        }

        var progressed = false;
        try
        {
            progressed = ProcessRoom(room, now);
        }
        catch (Exception e)
        {
            MovementCounters.RoomFault();
            ExceptionLogger.LogException(e);

            var unusable = room.Room?.GetGameMap() == null;
            if (unusable)
            {
                try
                {
                    lock (room.MovementLock)
                        room.Close();
                }
                catch { /* teardown must not throw twice */ }
                lock (_roomsLock)
                    _rooms.Remove(room);
                return;
            }

            // Still usable. Re-queue it on the watchdog so RecoverOrphans runs
            // and picks up any walker the throw left unscheduled.
            try
            {
                lock (room.MovementLock)
                {
                    room.NextWatchdogTick = now;
                    room.RefreshNextDue();
                }
            }
            catch { /* fall through to the normal re-queue below */ }
        }

        lock (_roomsLock)
        {
            if (room.Closed)
            {
                _rooms.Remove(room);
            }
            else
            {
                // The SNAPSHOT taken inside ProcessRoom while MovementLock was
                // held - never a fresh read of the walker heap from here. That
                // read raced every click: RequestMove sifts the heap under
                // MovementLock on a packet thread, so PeekDue could see Count
                // from before a Remove and _items[0] from after it, and
                // dereference null. The throw landed OUTSIDE the per-room try
                // above and killed the scheduler thread outright.
                var due = room.NextDueSnapshot;
                if (due == long.MaxValue)
                {
                    _rooms.Remove(room);
                }
                else
                {
                    // ZERO-WORK SPIN GUARD.
                    //
                    // A pass that did nothing and still wants to be due at an
                    // already-expired tick is, by definition, asking to be
                    // popped again immediately for another pass that will do
                    // nothing. That is the busy-spin shape, and it costs the
                    // WHOLE hotel, because one thread serves every room.
                    //
                    // With the slack now consistent inside ProcessRoom this
                    // should be unreachable, so it is a counted assertion rather
                    // than a retry: it names the room and shows up in
                    // :movementstats as spinGuards, instead of silently
                    // hammering the scheduler the way the last freeze did.
                    if (!progressed && due <= now + MovementSettings.TickSlackMs)
                    {
                        MovementCounters.SpinGuard(room.RoomId);
                        due = now + MovementSettings.TickSlackMs + 1;
                    }
                    _rooms.InsertOrUpdate(room, due); // never a bare Push
                }
            }
        }
    }

    /// <summary>
    /// One pass over one room. Returns TRUE if the pass actually did something -
    /// drained a walker, ran the watchdog, or sealed a frame.
    ///
    /// THE INVARIANT: a room popped as due must advance at least one of the three
    /// terms ComputeNextDue is the minimum of, or it will be re-queued at the
    /// same expired tick and popped again immediately. Every "is it time yet?"
    /// test below therefore uses the SAME TickSlackMs the pop used. When the pop
    /// said "due" with 2ms of slack and the work tests said "not yet" without it,
    /// the 1-2ms gap between them was a hole a room could fall into and never
    /// climb out of.
    /// </summary>
    private bool ProcessRoom(RoomMovement room, long now)
    {
        MovementCounters.RoomProcessed();
        var started = Stopwatch.GetTimestamp();
        var budgetTicks = (long)(MovementSettings.DrainBudgetUs * (Stopwatch.Frequency / 1_000_000.0));
        var progressed = false;

        lock (room.MovementLock)
        {
            if (room.Closed)
                return false;

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
                    progressed = true; // its due tick moved strictly forward
                    continue;
                }

                room.Walkers.Remove(walker);
                walker.Queued = false;
                drained++;
                progressed = true;
                MovementCounters.DrainedWalker();

                MovementController.AdvanceWalker(room, walker, walker.DueTick, now);
            }

            // B. WATCHDOG - the direct fix for V1's unrecoverable frozen avatar.
            if (now + MovementSettings.TickSlackMs >= room.NextWatchdogTick)
            {
                room.NextWatchdogTick = now + MovementSettings.WatchdogIntervalMs;
                MovementController.RecoverOrphans(room, now);
                progressed = true;
            }

            // C. SEAL. Staging -> one frame; handed to Q1, never sent here.
            if (room.HasImmediateWork ||
                (now + MovementSettings.TickSlackMs >= room.NextFlushTick && room.HasStagedWork))
            {
                progressed = true;
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
                    // serverNow is sampled HERE, at seal time, so the client's
                    // clock-offset estimate reflects real send latency.
                    MovementWorkQueues.EnqueueOutbound(room, frame, now);
                }
            }

            // D. PUBLISH THE NEXT DUE TICK while the movement lock is still
            //    held. ProcessRoomIsolated re-queues the room from this value,
            //    so the walker heap is only ever read by its owner.
            room.RefreshNextDue();
        }

        return progressed;
    }
}

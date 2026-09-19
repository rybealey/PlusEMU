using System.Collections.Concurrent;
using Plus.Core;

namespace Plus.HabboHotel.Rooms.Movement;

/// <summary>
/// pixelrp Movement V2 (A8): Q1, the outbound queue.
///
/// A PER-ROOM FIFO with exactly ONE active consumer, so per-room ordering is a
/// property of the queue rather than of the thread that fills it. That is what
/// lets the scheduler stay pure (I-5) - it seals a frame and hands it over,
/// never composing or sending.
///
/// The consumer is a DEDICATED THREAD, not the .NET ThreadPool. This is not a
/// stylistic choice: Game.cs:137-147 documents that on this 2-core VPS a pooled
/// continuation can wait indefinitely for a free worker, which is exactly the
/// starvation V2 exists to remove. Using Task.Run here would silently
/// reintroduce it on the outbound path.
///
/// THERE WAS A SECOND QUEUE, Q2, FOR TILE EVENTS. It is gone, and the reason
/// matters more than the deletion:
///
///   TILE EFFECTS RUN INLINE, on this thread, inside
///   RoomUserManager.ApplyMovementFrame. That method moves the user onto each
///   record's from-tile and fires UserWalksOffFurni / UserWalksOnFurni for the
///   move, under _cycleLock, in order with the commit.
///
/// Q2 was built to own that work so the scheduler could never block on a furni
/// callback, together with a "movement barrier" that held a walker at a
/// boundary until its tile events completed. Neither was ever switched on: the
/// barrier was never armed, and Q2's handler body was an empty block with a
/// comment saying effects would move there at cutover. So every committed edge
/// enqueued an item, woke a thread, took the room's MovementLock and signalled
/// the scheduler, to do nothing - while the effects it was meant to own were
/// already running here.
///
/// IF TILE EFFECTS EVER MOVE OFF THIS THREAD, THE BARRIER COMES BACK WITH
/// THEM. Without it a walker can commit past an edge whose effect has not run,
/// and one of those effects is a mid-route teleport. Note also that "wired is
/// off" is a CONTENT property and not a safety one: Item.UserWalksOnFurni
/// reaches GetWired().TriggerEvent with no enabled guard, Room.cs runs
/// GetWired().OnCycle() unconditionally, and 210 wf_* definitions exist in SQL
/// - they are merely absent from the catalog, so any already-placed wired item
/// fires today. The deleted design is in git history if it is needed again.
/// </summary>
public static class MovementWorkQueues
{
    /// <summary>
    /// ONE outbound thread, deliberately.
    ///
    /// This was 2, which broke the ordering guarantee this class is supposed to
    /// provide: both threads pulled from the SAME queue with no per-room
    /// affinity, so two frames for one room could be applied concurrently and
    /// land out of order. An older frame applied after a newer one rewinds
    /// user.X/Y and can clear "mv" - i.e. an avatar that freezes just after it
    /// starts moving.
    ///
    /// A single consumer makes ordering a property of the queue rather than of
    /// timing. Per-room affinity across several threads would also work and
    /// would scale further, but movement frames are cheap (apply + one
    /// broadcast) and correctness here matters far more than parallelism.
    /// </summary>
    private const int WorkerCount = 1;

    private static readonly ConcurrentQueue<(RoomMovement Room, MovementEdgeRecord[] Frame, long ServerNowMs)> OutboundRooms = new();
    private static readonly ManualResetEventSlim OutboundWake = new(false);

    private static readonly List<Thread> Workers = new();
    private static volatile bool _running;
    private static long _framesHandedOff;
    private static long _lastOutboundLoopMs;

    /// <summary>Frames handed from the scheduler to Q1. Health metric only.</summary>
    public static long FramesHandedOff => Interlocked.Read(ref _framesHandedOff);

    /// <summary>Frames sealed but not yet applied. Persistently &gt; 0 = Q1 is behind.</summary>
    public static int OutboundDepth => OutboundRooms.Count;

    /// <summary>ms since Q1 last completed a pass. Huge = the worker is wedged on _cycleLock.</summary>
    public static long OutboundAgeMs => SystemMovementClock.Instance.NowMs - Interlocked.Read(ref _lastOutboundLoopMs);

    /// <summary>Is the outbound thread alive? A dead Q1 freezes every avatar in the hotel.</summary>
    public static bool WorkersAlive
    {
        get
        {
            if (Workers.Count == 0)
                return false;
            foreach (var worker in Workers)
            {
                if (!worker.IsAlive)
                    return false;
            }
            return true;
        }
    }

    public static void Start()
    {
        if (_running)
            return;
        _running = true;

        for (var i = 0; i < WorkerCount; i++)
        {
            var outbound = new Thread(OutboundLoop)
            {
                IsBackground = true,
                Name = $"PixelRPMovementOut{i}"
            };
            outbound.Start();
            Workers.Add(outbound);
        }
    }

    public static void Stop()
    {
        _running = false;
        OutboundWake.Set();
        foreach (var worker in Workers)
            worker.Join(1000);
        Workers.Clear();
    }

    // ---- Q1: outbound -----------------------------------------------------

    /// <summary>
    /// Called by the scheduler under the room lock after sealing a frame.
    /// Enqueue only - the scheduler never composes or sends.
    /// </summary>
    public static void EnqueueOutbound(RoomMovement room, MovementEdgeRecord[] frame, long serverNowMs)
    {
        if (room.Closed || frame == null || frame.Length == 0)
            return;
        OutboundRooms.Enqueue((room, frame, serverNowMs));
        OutboundWake.Set();
    }

    private static void OutboundLoop()
    {
        while (_running)
        {
            try
            {
                OutboundPass();
            }
            catch (Exception e)
            {
                // Same rule as the scheduler: this thread is the hotel's only
                // path from a sealed frame to the wire, so it must not be
                // possible for it to exit.
                ExceptionLogger.LogCriticalException(e);
                Thread.Sleep(1);
            }
        }
    }

    private static void OutboundPass()
    {
        OutboundWake.Wait(50);
        OutboundWake.Reset();

        while (OutboundRooms.TryDequeue(out var item))
        {
            var room = item.Room;
            if (room.Closed)
                continue;
            try
            {
                // Apply the frame to RoomUser and broadcast. This runs under
                // RoomUserManager's _cycleLock - the SAME lock V1 used to
                // serialise Statusses/UpdateNeeded against the broadcast - so
                // there is exactly one writer per lock and no torn Dictionary.
                //
                // ApplyMovementFrame emits BOTH halves of the contract, in order:
                // the UserUpdate carrying "mv" first, then this frame's 4110
                // records with the authoritative timing the renderer needs.
                room.Room.GetRoomUserManager()?.ApplyMovementFrame(item.Frame, item.ServerNowMs);
                Interlocked.Increment(ref _framesHandedOff);
            }
            catch (Exception e)
            {
                ExceptionLogger.LogException(e);
            }
        }

        Interlocked.Exchange(ref _lastOutboundLoopMs, SystemMovementClock.Instance.NowMs);
    }
}

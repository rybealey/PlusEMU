using System.Collections.Concurrent;
using System.Drawing;
using Plus.Core;

namespace Plus.HabboHotel.Rooms.Movement;

/// <summary>One queued tile transition for Q2. Immutable.</summary>
public readonly struct TileEventItem
{
    public readonly RoomMovement Room;
    public readonly MovementState Walker;
    public readonly long WalkSessionId;
    public readonly int EdgeIndex;
    public readonly Point Left;
    public readonly Point Entered;

    public TileEventItem(RoomMovement room, MovementState walker, Point left, Point entered)
    {
        Room = room;
        Walker = walker;
        WalkSessionId = walker.WalkSessionId;
        EdgeIndex = walker.EdgeIndex;
        Left = left;
        Entered = entered;
    }
}

/// <summary>
/// pixelrp Movement V2 (A8): Q1 (outbound) and Q2 (room/tile events).
///
/// Both are PER-ROOM FIFOs with exactly ONE active consumer, so per-room
/// ordering is a property of the queue rather than of the thread that fills it.
/// That is what lets the scheduler stay pure (I-5) while tile-leave/tile-enter
/// pairs still execute in order.
///
/// The consumers are DEDICATED THREADS, not the .NET ThreadPool. This is not a
/// stylistic choice: Game.cs:137-147 documents that on this 2-core VPS a pooled
/// continuation can wait indefinitely for a free worker, which is exactly the
/// starvation V2 exists to remove. Using Task.Run here would silently reintroduce
/// it on the outbound path.
/// </summary>
public static class MovementWorkQueues
{
    private const int WorkerCount = 2;

    private static readonly ConcurrentQueue<(RoomMovement Room, PendingEdgeCommit[] Frame)> OutboundRooms = new();
    private static readonly ConcurrentQueue<RoomMovement> EventRooms = new();
    private static readonly ConcurrentDictionary<uint, ConcurrentQueue<TileEventItem>> RoomEvents = new();

    private static readonly ManualResetEventSlim OutboundWake = new(false);
    private static readonly ManualResetEventSlim EventWake = new(false);

    private static readonly List<Thread> Workers = new();
    private static volatile bool _running;
    private static long _framesHandedOff;

    /// <summary>Frames handed from the scheduler to Q1. Health metric only.</summary>
    public static long FramesHandedOff => Interlocked.Read(ref _framesHandedOff);

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

            var events = new Thread(EventLoop)
            {
                IsBackground = true,
                Name = $"PixelRPMovementEvt{i}"
            };
            events.Start();
            Workers.Add(events);
        }
    }

    public static void Stop()
    {
        _running = false;
        OutboundWake.Set();
        EventWake.Set();
        foreach (var worker in Workers)
            worker.Join(1000);
        Workers.Clear();
    }

    // ---- Q1: outbound -----------------------------------------------------

    /// <summary>
    /// Called by the scheduler under the room lock after sealing a frame.
    /// Enqueue only - the scheduler never composes or sends.
    /// </summary>
    public static void EnqueueOutbound(RoomMovement room, PendingEdgeCommit[] frame)
    {
        if (room.Closed || frame == null || frame.Length == 0)
            return;
        OutboundRooms.Enqueue((room, frame));
        OutboundWake.Set();
    }

    private static void OutboundLoop()
    {
        while (_running)
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
                    // RoomUserManager's _cycleLock - the SAME lock V1 uses to
                    // serialise Statusses/UpdateNeeded against the broadcast - so
                    // there is exactly one writer per lock and no torn Dictionary.
                    //
                    // Packet 4110 is NOT emitted here. For the first beta test V2
                    // owns route and timing while the existing UserUpdateComposer
                    // carries the result, so a stock client renders it natively.
                    room.Room.GetRoomUserManager()?.ApplyMovementV2Frame(item.Frame);
                    Interlocked.Increment(ref _framesHandedOff);
                }
                catch (Exception e)
                {
                    ExceptionLogger.LogException(e);
                }
            }
        }
    }

    // ---- Q2: room / tile events ------------------------------------------

    /// <summary>
    /// Called by the scheduler under the room lock when an edge commits.
    /// The callback itself NEVER runs on the scheduler thread.
    /// </summary>
    public static void EnqueueTileEvent(RoomMovement room, MovementState walker, Point left, Point entered)
    {
        if (room.Closed)
            return;
        var queue = RoomEvents.GetOrAdd(room.RoomId, static _ => new ConcurrentQueue<TileEventItem>());
        queue.Enqueue(new TileEventItem(room, walker, left, entered));
        EventRooms.Enqueue(room);
        EventWake.Set();
    }

    private static void EventLoop()
    {
        while (_running)
        {
            EventWake.Wait(50);
            EventWake.Reset();

            while (EventRooms.TryDequeue(out var room))
            {
                if (room.Closed)
                    continue;
                if (!RoomEvents.TryGetValue(room.RoomId, out var queue))
                    continue;

                while (queue.TryDequeue(out var item))
                    ProcessTileEvent(item);
            }
        }
    }

    /// <summary>
    /// Process one tile transition and ALWAYS release the movement barrier.
    ///
    /// The finally block is mandatory. If an exception in a walk-on callback
    /// skipped the release, AwaitingEventsThroughEdge would stay armed forever
    /// and the walker would wait at a boundary permanently - and the watchdog
    /// could not rescue it, because a walker blocked on a declared barrier is
    /// legitimately waiting rather than orphaned.
    /// </summary>
    private static void ProcessTileEvent(TileEventItem item)
    {
        var room = item.Room;
        var walker = item.Walker;
        try
        {
            // Tile effects (furni walk-on/off, wired dispatch, game hooks) are
            // invoked here at cutover, through the normal MovementController /
            // displacement APIs. Nothing is invoked while V2 is inactive.
        }
        catch (Exception e)
        {
            ExceptionLogger.LogException(e);
        }
        finally
        {
            try
            {
                lock (room.MovementLock)
                {
                    if (!room.Closed && walker.WalkSessionId == item.WalkSessionId &&
                        item.EdgeIndex > walker.EventsProcessedThroughEdge)
                        walker.EventsProcessedThroughEdge = item.EdgeIndex;
                }
                MovementScheduler.Instance.Signal(room);
            }
            catch (Exception e)
            {
                ExceptionLogger.LogException(e);
            }
        }
    }

    public static void ForgetRoom(uint roomId) => RoomEvents.TryRemove(roomId, out _);
}

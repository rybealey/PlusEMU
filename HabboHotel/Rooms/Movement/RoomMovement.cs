using Plus.HabboHotel.Rooms.PathFinding.V2;

namespace Plus.HabboHotel.Rooms.Movement;

/// <summary>
/// pixelrp Movement V2 (A6/A7/A8): everything the scheduler owns for ONE room.
///
/// Lifetime is tied to the Room. <see cref="Closed"/> is the teardown latch that
/// makes a hotel-wide scheduler safe: RoomManager.UnloadRoom disposes the
/// Gamemap (nulling _userMap, GameMap, Model and more), so a scheduler that
/// popped a disposed room and called into its map would take a
/// NullReferenceException and, being a single thread, freeze movement in EVERY
/// room. Close() is called BEFORE disposal, under the movement lock.
/// </summary>
public sealed class RoomMovement : IDueHeapNode
{
    /// <summary>
    /// Guards MovementState, the walker heap, staging and the path scratch.
    /// Held only for in-memory work: no DB, socket, callback or blocking work
    /// may run under it (I-5 / I-14).
    /// </summary>
    public readonly object MovementLock = new();

    public readonly Room Room;
    public readonly uint RoomId;

    /// <summary>Walkers with a live scheduler entry. At most one entry each (I-1).</summary>
    public readonly IndexedDueHeap<MovementState> Walkers = new();

    /// <summary>
    /// Every unit in this room that V2 knows about, keyed by VirtualId.
    /// Distinct from <see cref="Walkers"/>: this is membership, that is
    /// scheduling. The watchdog compares the two to find orphans.
    /// Guarded by <see cref="MovementLock"/>.
    /// </summary>
    public readonly Dictionary<int, MovementState> States = new();

    /// <summary>Per-room A* working set; reused across searches, no per-search allocation.</summary>
    public readonly PathScratch Scratch;

    // ---- room heap slot (owned by the scheduler's room heap) --------------
    public int HeapIndex { get; set; } = -1;
    public long DueTick { get; set; }

    // ---- lifecycle --------------------------------------------------------
    private int _closed;
    public bool Closed => Volatile.Read(ref _closed) != 0;

    // ---- scheduling bookkeeping ------------------------------------------
    public long NextFlushTick;
    public long NextWatchdogTick;

    /// <summary>
    /// Edge commits sealed into the current frame, waiting for the Q1 worker to
    /// apply them to RoomUser and broadcast.
    ///
    /// The scheduler NEVER writes RoomUser itself. V1 serialises every
    /// Statusses/UpdateNeeded write against SerializeStatusUpdates using
    /// RoomUserManager's _cycleLock; a second thread writing those same fields
    /// under a different lock would race a plain Dictionary. Staging here and
    /// applying under _cycleLock on the worker keeps one writer per lock and
    /// keeps I-5 intact (no socket work on the scheduler).
    /// </summary>
    public readonly List<PendingEdgeCommit> Staged = new();

    /// <summary>Set by Signal(); cleared when the scheduler picks the room up.</summary>
    private int _signalPending;

    /// <summary>True while staged work is waiting to be sealed into a frame.</summary>
    public bool HasStagedWork;

    /// <summary>True while at least one staged item is latency-critical (a click / redirect).</summary>
    public bool HasImmediateWork;

    /// <summary>Per-room monotonic frame counter. Debug/assert only - never on the wire.</summary>
    public long FrameSequence;

    public RoomMovement(Room room)
    {
        Room = room;
        RoomId = room.RoomId;
        var model = room.GetGameMap()?.Model;
        var width = model?.MapSizeX ?? 1;
        var height = model?.MapSizeY ?? 1;
        Scratch = new PathScratch(Math.Max(1, width), Math.Max(1, height));
    }

    public bool TrySetSignalPending() => Interlocked.CompareExchange(ref _signalPending, 1, 0) == 0;

    public void ClearSignalPending() => Interlocked.Exchange(ref _signalPending, 0);

    /// <summary>
    /// Teardown latch (A7). MUST be called under MovementLock and BEFORE the
    /// room's Gamemap is disposed. Removal from the scheduler's room heap is
    /// performed by the scheduler itself, which owns that heap.
    /// </summary>
    public void Close()
    {
        Interlocked.Exchange(ref _closed, 1);
        Walkers.Clear();
        HasStagedWork = false;
        HasImmediateWork = false;
    }

    /// <summary>
    /// Earliest tick at which this room needs the scheduler again, or
    /// long.MaxValue when it needs nothing.
    ///
    /// The Staging term matters: without it, the last walker's deferred
    /// walk-end frame is stranded and the orphan watchdog stops running.
    /// </summary>
    public long ComputeNextDue()
    {
        if (Closed)
            return long.MaxValue;

        var due = Walkers.PeekDue;
        if (HasStagedWork && NextFlushTick < due)
            due = NextFlushTick;
        if (NextWatchdogTick < due)
            due = NextWatchdogTick;
        return due;
    }

    public bool HasWork => !Closed && ComputeNextDue() != long.MaxValue;
}

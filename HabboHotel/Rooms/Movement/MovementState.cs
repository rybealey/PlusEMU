using System.Drawing;
using Plus.HabboHotel.Rooms.PathFinding.V2;

namespace Plus.HabboHotel.Rooms.Movement;

public enum MovementMode : byte
{
    /// <summary>At rest on Tile. No scheduler entry.</summary>
    Standing = 0,

    /// <summary>An edge is in flight. EXACTLY ONE scheduler entry (I-1).</summary>
    Moving = 1,

    /// <summary>Cannot walk (knockout / freeze). At rest, no scheduler entry.</summary>
    Blocked = 2,

    /// <summary>Transient: teleport / roller this pass; resolves to Standing.</summary>
    Displaced = 3
}

/// <summary>
/// pixelrp Movement V2 (A4): the complete per-avatar movement state.
///
/// Replaces roughly 25 scattered RoomUser fields. Every field here has exactly
/// ONE writer, which is the property V1 lacked: it stored a step's destination
/// in three places at once (SetX/SetY, Statusses["mv"], and the Path cursor).
///
/// DELIBERATELY ABSENT - do not add these back:
///   MovementSeq       identity is (WalkSessionId, RouteRevision, EdgeIndex),
///                     proven a total order; a second counter can only disagree
///   PromiseBuffer     superseded by EmittedThroughEdge + RouteBuffer.BaseIndex
///                     + the COMMIT-BEFORE-REPLACE rule (LOCK NOTE 2.2)
///   Formation*        no pairwise formation system exists in V2
///   TimingGroupId /
///   GroupAffinity     replaced by the phase-snap (LOCK NOTE 2.6)
///   WalkGeneration /
///   SelfPaced         replaced by "exactly one scheduler entry"
///   FastWalking       V2 is exactly one validated tile per 500ms
/// </summary>
public sealed class MovementState : IDueHeapNode
{
    public int VirtualId;

    // ---- scheduler queue slot (owned by IndexedDueHeap) --------------------
    /// <summary>Owned by the room's walker heap. -1 = not scheduled.</summary>
    public int HeapIndex { get; set; } = -1;

    /// <summary>Absolute movement-clock tick at which this walker's next commit is due.</summary>
    public long DueTick { get; set; }

    // ---- identity ---------------------------------------------------------
    /// <summary>++ on Standing->Moving and on every displacement. Never reset within a RoomUser.</summary>
    public long WalkSessionId;

    /// <summary>0 at session start; ++ per re-plan within the session.</summary>
    public int RouteRevision;

    /// <summary>0 at session start; ++ once per committed edge.</summary>
    public int EdgeIndex;

    // ---- mode -------------------------------------------------------------
    public MovementMode Mode = MovementMode.Standing;

    // ---- timeline ---------------------------------------------------------
    /// <summary>
    /// Absolute movement-clock tick of edge 0 of this session. Every edge start
    /// is DERIVED as TimelineOrigin + k * IntervalMs and is never stored:
    /// storing a precomputed "next step tick" is exactly how V1 leaked stale
    /// schedules into later walks.
    /// </summary>
    public long TimelineOrigin;

    // ---- geometry ---------------------------------------------------------
    public Point Tile;
    public double TileZ;
    public Point EdgeTo;
    public double EdgeToZ;
    public Point Target;
    public byte Facing;

    // ---- route ------------------------------------------------------------
    public readonly RouteBuffer Route = new();

    // ---- promises ---------------------------------------------------------
    /// <summary>
    /// Highest edge index already PROMISED on the wire for the current
    /// (session, revision), counting advertised lookahead. Timing for every
    /// index up to here is immutable (I-3).
    /// </summary>
    public int EmittedThroughEdge = -1;

    // ---- movement-critical tile barrier (A9) ------------------------------
    /// <summary>Edge index whose tile events must complete before the NEXT commit. -1 = none.</summary>
    public int AwaitingEventsThroughEdge = -1;

    /// <summary>Highest edge index whose tile events Q2 has finished processing.</summary>
    public int EventsProcessedThroughEdge = -1;

    // ---- bookkeeping ------------------------------------------------------
    public long LastRepathAtMs = long.MinValue;
    public Point LastRepathTarget;

    /// <summary>Set while this walker has a live scheduler queue entry (I-1).</summary>
    public bool Queued;

    public void ResetForNewSession(long nowMs, Point tile, double tileZ)
    {
        WalkSessionId++;
        RouteRevision = 0;
        EdgeIndex = 0;
        TimelineOrigin = nowMs;
        EmittedThroughEdge = -1;
        AwaitingEventsThroughEdge = -1;
        EventsProcessedThroughEdge = -1;
        Tile = tile;
        TileZ = tileZ;
        EdgeTo = tile;
        EdgeToZ = tileZ;
        Route.Clear();
    }

    /// <summary>
    /// THE elapsing-edge derivation. LOCK NOTE 2.2 requires exactly ONE of
    /// these to exist - revision 2 of the architecture had three subtly
    /// different versions across sections 4.4, 4.6 and 13, which produced an
    /// off-by-one that made the post-stall edge undeliverable.
    ///
    ///     e = floor((now - TimelineOrigin) / 500)
    ///
    /// Derived from the TIMELINE, never from "the last edge the scheduler
    /// committed" (the scheduler may be behind) and never from "the last
    /// promised edge" (that is the far end of lookahead).
    ///
    ///   index &lt; e   historical, already elapsed, NEVER corrected
    ///   index == e  elapsing, geometry immutable, MUST finish
    ///   index &gt; e   future, timing fixed, geometry replaceable by a revision
    /// </summary>
    public int ElapsingEdgeIndex(long nowMs)
    {
        var delta = nowMs - TimelineOrigin;
        if (delta <= 0)
            return 0;
        return (int)(delta / MovementSettings.IntervalMs);
    }

    /// <summary>Absolute start tick of an edge index on this session's timeline.</summary>
    public long EdgeStartTick(int edgeIndex) =>
        TimelineOrigin + (long)edgeIndex * MovementSettings.IntervalMs;

    /// <summary>True when the barrier from A9 currently blocks committing the next edge.</summary>
    public bool BarrierBlocks(int nextEdgeIndex) =>
        AwaitingEventsThroughEdge >= 0 &&
        EventsProcessedThroughEdge < AwaitingEventsThroughEdge &&
        nextEdgeIndex > AwaitingEventsThroughEdge;
}

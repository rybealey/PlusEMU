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
    Displaced = 3,

    /// <summary>
    /// Enrolled and routed, waiting for a phase boundary that is at most
    /// MaxStartDelayMs away. NOTHING has been emitted: no "mv", no 4110, no
    /// staged edge. The walker holds exactly one scheduler entry, due at
    /// TimelineOrigin, and becomes Moving on that beat.
    ///
    /// Staging early with a future cycleStart would NOT work: ApplyMovementFrame
    /// sets "mv" as soon as a frame is applied, and native Nitro would start
    /// lerping the avatar while the V2 store still has no edge covering `now` -
    /// so it would drift natively and then snap when V2 took over.
    /// </summary>
    Pending = 4
}

/// <summary>How a walk start resolved against the room's movement phase.</summary>
public enum PhaseDecision : byte
{
    None = 0,

    /// <summary>No live phase; this walk established one.</summary>
    Established = 1,

    /// <summary>Joined an existing phase, waiting up to MaxStartDelayMs.</summary>
    Aligned = 2,

    /// <summary>Boundary was too far off; started immediately, unaligned.</summary>
    Skipped = 3
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

    // ---- escort shadow (pixelrp police) -----------------------------------
    /// <summary>
    /// "No shadow", for both fields below.
    ///
    /// It is NOT zero, and that is the whole point: RoomUserManager hands out
    /// virtual ids from 0 upwards (_primaryPrivateUserId++), so unit 0 is a
    /// real avatar - whoever walked into the room first. While 0 doubled as
    /// the empty value, escorting that avatar wrote ShadowVirtualId = 0 onto
    /// their captor, StageShadow read it as "this walker has no shadow" and
    /// bailed on its first line, and the suspect was seated, frozen and never
    /// moved again. It looked like a client bug for exactly as long as nobody
    /// noticed which avatar had entered the room first.
    /// </summary>
    public const int NoShadow = -1;

    /// <summary>
    /// VirtualId of the unit this walker marches one tile ahead of itself, or
    /// <see cref="NoShadow"/>.
    /// The shadow has NO route of its own: every edge this walker stages, a
    /// matching one is staged for the shadow with identical timing (see
    /// MovementController.StageShadow). That is what keeps an escorted suspect
    /// in lockstep - the pathfinder is never consulted for them at all.
    /// Guarded by MovementLock like every other field here.
    /// </summary>
    public int ShadowVirtualId = NoShadow;

    /// <summary>
    /// VirtualId of the walker this unit is the shadow of, or
    /// <see cref="NoShadow"/>. While set, this
    /// unit's own walk requests are refused at RequestMove - nothing may give a
    /// shadowed unit a route while something else is deciding where it goes.
    /// </summary>
    public int ShadowedBy = NoShadow;


    // ---- promises ---------------------------------------------------------
    /// <summary>
    /// Highest edge index for which a REAL 4110 record has been staged in this
    /// session. Starts at -1, and only ever rises within a session.
    ///
    /// IT DOES NOT COUNT ADVERTISED LOOKAHEAD, and that is the whole point of
    /// this comment. StageEdge raises it to w.EdgeIndex only - the index of the
    /// record being staged - while that same record carries lookahead for up to
    /// MovementSettings.LookaheadMax FURTHER indexes. So the client reliably
    /// knows the geometry of indexes ABOVE this value, and begins rendering
    /// them from that lookahead the instant their cycleStart passes.
    ///
    /// The previous wording here said it counted lookahead. It never has, and
    /// the difference is not academic: it made "a redirect only ever restages
    /// EmittedThroughEdge + 1, so it never touches an emitted edge" read as a
    /// safety argument, when the index being restaged is one the client may
    /// already be drawing.
    ///
    /// NOTHING GATES ON IT. It is not an enforced immutability boundary, and no
    /// code consults it before replacing geometry. It has exactly two readers:
    ///
    ///   SyncCommitsTo  bounds the silent-commit loop, so a walker is never
    ///                  advanced past an index for which nothing was put on the
    ///                  wire
    ///   StopWalk       the `neverEmitted` test - a Pending walker that emitted
    ///                  nothing needs no walk-end, because the client was never
    ///                  told the unit was moving
    ///
    /// Nor does it govern timing. Every edge start is DERIVED as
    /// TimelineOrigin + k * IntervalMs, so timing immutability comes from
    /// TimelineOrigin not moving - not from this field.
    ///
    /// StageCorrection also raises it to (fromEdgeIndex - 1), which can assert
    /// an index that was never individually staged. That is deliberate: it is
    /// what lets SyncCommitsTo reach the elapsing edge on a later beat. It does
    /// mean the value reads as "the commit loop may advance this far", not as a
    /// literal record of what went on the wire.
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

    // ---- deferred redirect ------------------------------------------------
    /// <summary>
    /// A redirect target held back because the walker had not yet caught up to
    /// the elapsing edge index.
    ///
    /// Planning while EdgeIndex &lt; e labels the route BaseIndex = e + 1 while
    /// planning it from EdgeTo - the terminal of an EARLIER edge - so every
    /// index in it is wrong by (e - EdgeIndex) and the chain acquires a hole.
    /// The click is kept here and retried on a later beat instead of being
    /// dropped, because the commit path is the only thing that brings EdgeIndex
    /// forward.
    /// </summary>
    public bool HasDeferredRedirect;

    public Point DeferredRedirectTarget;

    // ---- early correction publish (experiment) ----------------------------
    /// <summary>
    /// Identity of the last edge published early by StageCorrection, so the
    /// same (session, revision, index) is never transmitted twice from there.
    /// Deliberately does NOT suppress the normal boundary stage for that index:
    /// that record performs the commit and carries the refreshed lookahead.
    /// </summary>
    public long LastEarlyPublishSession = -1;
    public int LastEarlyPublishRevision = -1;
    public int LastEarlyPublishEdge = -1;

    /// <summary>
    /// Real players only establish and hold the room phase. Bots and pets walk
    /// on their own timelines and are ignored entirely for alignment: a patrol
    /// bot is almost always moving, so letting one hold the phase would charge
    /// every player click the alignment wait, permanently.
    /// </summary>
    public bool IsRealUser;

    // ---- diagnostics: how the last walk start resolved --------------------
    public PhaseDecision LastPhaseDecision;
    public int LastStartDelayMs;

    /// <summary>
    /// True while this walker holds a scheduler queue entry (I-1).
    ///
    /// DERIVED, never assigned. IndexedDueHeap owns HeapIndex and is the only
    /// thing that may change it: InsertOrUpdate sets it, Remove clears it to -1
    /// on EVERY path including the not-present one, Pop goes through Remove,
    /// and Clear and Swap maintain it across every move. So "is this walker
    /// queued" has exactly one source of truth, and a second copy cannot drift
    /// out of step with it.
    ///
    /// This was a bool assigned by hand at seven sites across four files, each
    /// one sitting immediately next to the heap call that had already decided
    /// the answer.
    /// </summary>
    public bool Queued => HeapIndex >= 0;

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
        HasDeferredRedirect = false;
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

using System.Drawing;
using Plus.HabboHotel.Rooms.PathFinding;
using Plus.HabboHotel.Rooms.PathFinding.V2;

namespace Plus.HabboHotel.Rooms.Movement;

/// <summary>
/// pixelrp Movement V2 (A5): route planning, redirects and edge commits.
///
/// THE REDIRECT RULE, which is the single most important behaviour in V2:
/// a redirect changes the ROUTE, never the CLOCK. RouteRevision increments;
/// WalkSessionId, TimelineOrigin, EdgeIndex and timing alignment are untouched
/// (I-2). That one separation is what deletes V1's entire formation
/// re-admission machinery, in which a mid-walk redirect could cost a further
/// full 500ms beat whenever admission failed.
///
/// The elapsing index is derived in exactly ONE place -
/// MovementState.ElapsingEdgeIndex - never re-derived locally.
/// </summary>
public static class MovementController
{
    /// <summary>
    /// Standing -> Moving. Zero added latency: the timeline starts NOW.
    /// Returns false when no route exists (nothing is emitted in that case).
    /// </summary>
    public static bool StartWalk(
        RoomMovement room, MovementState w, Point target, in TraverseContext ctx,
        long nowMs, bool allowPartial = true)
    {
        if (room.Closed)
            return false;

        var map = room.Room.GetGameMap();
        if (map == null)
            return false;

        var result = AStarPathfinder.FindRoute(
            map, room.Scratch, w.Route, w.Tile, target, ctx,
            baseIndex: 0, allowPartial: allowPartial);

        if (result == PathResult.None || !w.Route.HasNext)
            return false;

        var tile = w.Tile;
        var tileZ = w.TileZ;

        // THE ROOM ACTIVE PHASE ANCHOR. Caller holds MovementLock, so two
        // simultaneous Standing->Moving requests cannot establish two phases.
        var origin = ResolveStartOrigin(room, w, nowMs);

        w.WalkSessionId++;
        w.RouteRevision = 0;
        w.EdgeIndex = 0;
        w.TimelineOrigin = origin;
        w.EmittedThroughEdge = -1;
        w.AwaitingEventsThroughEdge = -1;
        w.EventsProcessedThroughEdge = -1;
        w.Target = target;
        w.Tile = tile;
        w.TileZ = tileZ;

        MovementCounters.WalkStart();

        if (origin > nowMs)
        {
            // Joining the phase. The route is planned, but NOTHING is emitted
            // until the boundary arrives - see MovementMode.Pending.
            w.Mode = MovementMode.Pending;
            room.Walkers.InsertOrUpdate(w, origin);
            w.Queued = true;
            return true;
        }

        w.Mode = MovementMode.Moving;
        PlanNextEdge(room, w, map, ctx, nowMs, immediate: true);
        return w.Mode == MovementMode.Moving;
    }

    /// <summary>
    /// Pick this walk's TimelineOrigin, joining the room's movement phase when
    /// that costs at most <see cref="MovementSettings.MaxStartDelayMs"/>.
    ///
    /// With the ceiling at IntervalMs this ALWAYS joins, because the distance to
    /// the next boundary is 0..499 and therefore never exceeds it. Alignment is
    /// then guaranteed rather than opportunistic, at a cost of up to 499ms of
    /// input latency (~250ms average) on any walk begun while someone else is
    /// already walking. Lowering the ceiling reverts to opportunistic with no
    /// other change.
    ///
    /// Snapping BACKWARD is not an option: edge 0 would already be part-elapsed
    /// when emitted, so the client would render the avatar instantly a fraction
    /// of a tile along. Alignment must never move an avatar.
    ///
    /// The boundary is honoured EXACTLY even when the scheduler runs early or
    /// late, because edge 0's cycleStart is derived from TimelineOrigin rather
    /// than from the tick the beat happened to fire on.
    ///
    /// Caller MUST hold MovementLock.
    /// </summary>
    private static long ResolveStartOrigin(RoomMovement room, MovementState w, long nowMs)
    {
        w.LastStartDelayMs = 0;

        // Bots and pets neither establish, hold nor follow a phase. A patrol bot
        // is almost always moving, so letting one hold the phase would charge
        // every player click the alignment wait, permanently.
        if (!w.IsRealUser)
        {
            w.LastPhaseDecision = PhaseDecision.None;
            return nowMs;
        }

        if (!HasLivePhase(room, w))
        {
            room.PhaseAnchor = nowMs;
            w.LastPhaseDecision = PhaseDecision.Established;
            return nowMs;
        }

        var interval = MovementSettings.IntervalMs;
        var delta = ((room.PhaseAnchor - nowMs) % interval + interval) % interval;

        if (delta == 0)
        {
            // Already exactly on the boundary: aligned at zero cost.
            w.LastPhaseDecision = PhaseDecision.Aligned;
            return nowMs;
        }

        if (delta <= MovementSettings.MaxStartDelayMs)
        {
            w.LastPhaseDecision = PhaseDecision.Aligned;
            w.LastStartDelayMs = (int)delta;
            return nowMs + delta;
        }

        w.LastPhaseDecision = PhaseDecision.Skipped;
        return nowMs;
    }

    /// <summary>
    /// Is any OTHER real user currently holding the room's phase?
    ///
    /// DERIVED BY SCANNING, never counted. A missed decrement on some exit from
    /// Moving would clear the phase while avatars were still walking, and the
    /// next walker would establish a different one - reintroducing the
    /// misalignment intermittently, which is far harder to see than having it
    /// all the time.
    ///
    /// Caller MUST hold MovementLock.
    /// </summary>
    private static bool HasLivePhase(RoomMovement room, MovementState self)
    {
        foreach (var other in room.States.Values)
        {
            if (ReferenceEquals(other, self) || !other.IsRealUser)
                continue;
            if (other.Mode == MovementMode.Moving || other.Mode == MovementMode.Pending)
                return true;
        }
        return false;
    }

    /// <summary>
    /// A click that lands while the walker is still Pending. Nothing has been
    /// emitted yet, so the route is replaced in place: the timeline, the session
    /// and the phase boundary all stand.
    /// </summary>
    public static bool RepathPending(
        RoomMovement room, MovementState w, Point target, in TraverseContext ctx, long nowMs)
    {
        if (room.Closed || w.Mode != MovementMode.Pending)
            return false;

        var map = room.Room.GetGameMap();
        if (map == null)
            return false;

        var result = AStarPathfinder.FindRoute(
            map, room.Scratch, w.Route, w.Tile, target, ctx,
            baseIndex: 0, allowPartial: true);

        if (result == PathResult.None || !w.Route.HasNext)
        {
            StopWalk(room, w);
            return false;
        }

        w.Target = target;
        w.LastRepathAtMs = nowMs;
        w.LastRepathTarget = target;
        return true;
    }

    /// <summary>
    /// Moving -> Moving. THE redirect. See the class remarks and LOCK NOTE 2.2.
    /// </summary>
    public static bool Redirect(
        RoomMovement room, MovementState w, Point target, in TraverseContext ctx,
        long nowMs, bool allowPartial = true)
    {
        if (room.Closed || w.Mode != MovementMode.Moving)
            return false;

        // Identical-target debounce, so spam-clicking one tile cannot spin A*.
        // Target-specific by design: a redirect to a DIFFERENT tile is never
        // delayed by it.
        if (w.LastRepathTarget == target &&
            nowMs - w.LastRepathAtMs < MovementSettings.RepathMinIntervalMs)
            return false;

        var map = room.Room.GetGameMap();
        if (map == null)
            return false;

        // 1. Derive the elapsing edge from the TIMELINE (single helper).
        var e = w.ElapsingEdgeIndex(nowMs);

        // 2. COMMIT-BEFORE-REPLACE (rule R1). The route must not be replaced
        //    until the walker has been synced to the elapsing index, or the
        //    geometry of already-elapsed promised edges is lost and the commit
        //    path has nothing to read. This ordering is what makes a separate
        //    PromiseBuffer unnecessary.
        SyncCommitsTo(room, w, e, nowMs);
        if (w.Mode != MovementMode.Moving)
            return false;

        // 3. Origin = terminal of the CURRENT ELAPSING EDGE.
        //    NOT the last promised terminal: that would force the avatar to
        //    walk to the end of advertised lookahead (up to 1500ms) before
        //    turning, which is precisely the responsiveness bug this rule fixes.
        var origin = w.EdgeTo;

        // 4. Plan from that origin.
        var result = AStarPathfinder.FindRoute(
            map, room.Scratch, w.Route, origin, target, ctx,
            baseIndex: e + 1, allowPartial: allowPartial);

        if (result == PathResult.None || !w.Route.HasNext)
            return false; // keep walking the existing route

        // 5/6/7. Route identity advances; the movement clock does not.
        MovementCounters.Redirect();
        w.RouteRevision++;
        w.Target = target;
        w.LastRepathAtMs = nowMs;
        w.LastRepathTarget = target;
        // UNCHANGED, deliberately: WalkSessionId, TimelineOrigin, EdgeIndex,
        //                          DueTick / queue entry, timing alignment.

        // 8. Future indexes (> e) may be restaged; indexes <= e never change.
        StageCorrection(room, w, e + 1);
        return true;
    }

    /// <summary>
    /// Advance the walker to the elapsing index by committing edges that have
    /// ALREADY ELAPSED on the client, emitting nothing.
    ///
    /// This is the server reconciling to promises it already made - not
    /// catch-up motion. Nothing accelerates: the client walked those tiles in
    /// real time while the server was behind.
    /// </summary>
    public static void SyncCommitsTo(RoomMovement room, MovementState w, int elapsingIndex, long nowMs)
    {
        var guard = 0;
        while (w.Mode == MovementMode.Moving
               && w.EdgeIndex < elapsingIndex
               && w.EdgeIndex < w.EmittedThroughEdge
               && guard++ < MovementSettings.MaxDrainPerRoom)
        {
            if (!CommitEdgeSilently(room, w, nowMs))
                break;
        }
    }

    /// <summary>
    /// Commit the in-flight edge: Tile becomes EdgeTo, tile events are queued
    /// for Q2 (never executed here), and EdgeIndex advances. Emits nothing.
    /// </summary>
    private static bool CommitEdgeSilently(RoomMovement room, MovementState w, long nowMs)
    {
        if (w.Mode != MovementMode.Moving)
            return false;

        MovementCounters.Commit();
        var previous = w.Tile;
        w.Tile = w.EdgeTo;
        w.TileZ = w.EdgeToZ;
        w.EdgeIndex++;

        QueueTileEvents(room, w, previous, w.Tile);
        return true;
    }

    /// <summary>
    /// One scheduler beat for one walker: commit the finished edge, honour
    /// promises made during any lateness, then plan and stage the next edge.
    /// </summary>
    public static void AdvanceWalker(RoomMovement room, MovementState w, long scheduledTick, long nowMs)
    {
        if (room.Closed || (w.Mode != MovementMode.Moving && w.Mode != MovementMode.Pending))
            return;

        var map = room.Room.GetGameMap();
        if (map == null)
        {
            StopWalk(room, w);
            return;
        }

        // The Pending boundary has arrived: this is the walk's FIRST beat.
        // Deliberately no commit - no edge was ever staged, so there is nothing
        // to commit and nothing elapsed to reconcile against.
        if (w.Mode == MovementMode.Pending)
        {
            w.Mode = MovementMode.Moving;
            var startCtx = new TraverseContext(cornerPolicy: CornerPolicy.Off);
            PlanNextEdge(room, w, map, startCtx, nowMs, immediate: true);
            return;
        }

        MovementCounters.Advance();
        var lateMs = nowMs - scheduledTick;
        if (lateMs > MovementSettings.IntervalMs)
            MovementCounters.BeatLate(lateMs);

        // (a) commit the edge that just finished
        CommitEdgeSilently(room, w, nowMs);

        // (b) lateness: honour promises, never contradict them.
        if (lateMs > MovementSettings.IntervalMs)
        {
            var elapsing = w.ElapsingEdgeIndex(nowMs);
            SyncCommitsTo(room, w, elapsing, nowMs);
        }

        // (c) plan the next edge
        var ctx = new TraverseContext(cornerPolicy: CornerPolicy.Off);
        PlanNextEdge(room, w, map, ctx, nowMs, immediate: false);
    }

    /// <summary>
    /// Select, validate and stage the next edge, then re-queue the walker at
    /// its NEXT timeline boundary - never at "now + 500", so a late scheduler
    /// produces a late packet and never a shifted timeline.
    /// </summary>
    private static void PlanNextEdge(
        RoomMovement room, MovementState w, Gamemap map, in TraverseContext ctx,
        long nowMs, bool immediate)
    {
        if (!w.Route.HasNext)
        {
            MovementCounters.StopRouteEnd();
            StopWalk(room, w);
            return;
        }

        var next = w.Route.PeekNext();
        var isFinal = w.Route.IsLast;
        var verdict = CanTraverse.Evaluate(map, w.Tile, next, isFinal, ctx);

        if (!CanTraverse.IsPassable(verdict, isFinal))
        {
            // Blocked at commit: re-plan from the CURRENT tile (EdgeTo is
            // unusable) and bump the revision. Failure ends the walk cleanly.
            MovementCounters.Replan();
            var replanned = AStarPathfinder.FindRoute(
                map, room.Scratch, w.Route, w.Tile, w.Target, ctx,
                baseIndex: w.EdgeIndex, allowPartial: true);
            if (replanned == PathResult.None || !w.Route.HasNext)
            {
                MovementCounters.StopBlocked();
                StopWalk(room, w);
                return;
            }
            w.RouteRevision++;
            next = w.Route.PeekNext();
            isFinal = w.Route.IsLast;
        }

        w.Route.Advance();
        w.EdgeTo = next;
        w.EdgeToZ = map.SqAbsoluteHeight(next.X, next.Y);
        w.Facing = (byte)Rotation.Calculate(w.Tile.X, w.Tile.Y, next.X, next.Y);

        // The movement-critical tile barrier (A9) is DELIBERATELY NOT ARMED yet.
        //
        // Two reasons, both discovered on the first beta test:
        //
        // 1. IT IS REDUNDANT IN THIS BUILD. ApplyMovementV2Frame mirrors V1 and
        //    fires UserWalksOffFurni / UserWalksOnFurni inline under _cycleLock,
        //    so tile effects already run in order with the commit. The Q2
        //    handler body is still empty (effects move there at cutover), so
        //    arming the barrier gates on events that do nothing.
        //
        // 2. ARMING IT HERE SELF-BLOCKS. Arming at PLAN time with w.EdgeIndex
        //    means the scheduler's pre-commit check, BarrierBlocks(EdgeIndex+1),
        //    is already true on the next beat - but the only thing that queues
        //    edge k's tile event is CommitEdgeSilently, inside the very
        //    AdvanceWalker call the barrier just blocked. The avatar freezes on
        //    its first step and the room spins hot.
        //
        // When Q2 owns tile effects at cutover, arm it at COMMIT time (after
        // EdgeIndex++ in CommitEdgeSilently) so the event is queued before the
        // barrier can block anything, and re-check the drain-loop condition.

        StageEdge(room, w, immediate);

        var nextDue = w.EdgeStartTick(w.EdgeIndex) + MovementSettings.IntervalMs;
        room.Walkers.InsertOrUpdate(w, nextDue); // never a bare Push (I-1)
        w.Queued = true;
    }

    public static void StopWalk(RoomMovement room, MovementState w)
    {
        if (w.Queued)
        {
            room.Walkers.Remove(w);
            w.Queued = false;
        }

        // A walker abandoned while still Pending never put anything on the wire,
        // so there is nothing to close off. A walk-end here would tell the client
        // to forget a unit it was never told about.
        var neverEmitted = w.Mode == MovementMode.Pending && w.EmittedThroughEdge < 0;

        w.Mode = MovementMode.Standing;
        w.EdgeTo = w.Tile;
        w.EdgeToZ = w.TileZ;
        w.Route.Clear();
        w.AwaitingEventsThroughEdge = -1;

        if (!neverEmitted)
            StageEdge(room, w, immediate: false); // walk-end marker slot
    }

    /// <summary>
    /// Watchdog (I-12): a walker in Mode == Moving with no scheduler entry is
    /// unreachable and would be frozen forever. V1 had exactly this failure
    /// (SelfPaced set true before the task was guaranteed to run) with no
    /// recovery path at all; here it is repaired within a second and counted
    /// as the defect it is.
    /// </summary>
    public static void RecoverOrphans(RoomMovement room, long nowMs)
    {
        if (room.Closed)
            return;
        foreach (var walker in MovementRegistry.WalkersOf(room))
        {
            // PENDING IS COVERED TOO, and must be. A Pending walker that lost
            // its scheduler entry has emitted nothing and can never emit
            // anything, so without this it stands still forever with no symptom
            // to read - the same unrecoverable shape this watchdog exists for.
            if (walker.Mode != MovementMode.Moving && walker.Mode != MovementMode.Pending)
                continue;
            if (room.Walkers.Contains(walker))
                continue;
            MovementCounters.OrphanRecovered();

            // Honour a boundary still in the future; never bring a start forward.
            var due = walker.Mode == MovementMode.Pending
                ? Math.Max(nowMs, walker.TimelineOrigin)
                : nowMs;
            room.Walkers.InsertOrUpdate(walker, due);
            walker.Queued = true;
        }
    }

    // ---- staging ----------------------------------------------------------
    // Frame CONTENT (the UserUpdate "mv" entry followed by its 4110 record) is
    // wired at cutover. While V2 is inactive these mark the room as having work
    // so the seal/flush path and its cadence can be exercised and measured.

    /// <summary>
    /// Seal one edge into an immutable wire record.
    ///
    /// This REPLACES the old V2 -> V1 bridge, which staged only enough to mirror
    /// V1's RoomUser fields and then relied on V1's UserUpdate broadcast to
    /// render. The record now carries the full 4110 contract - identity, absolute
    /// timing and lookahead - so V2 owns rendering outright.
    /// </summary>
    private static void StageEdge(RoomMovement room, MovementState w, bool immediate)
    {
        room.HasStagedWork = true;
        if (immediate)
            room.HasImmediateWork = true;
        if (w.EdgeIndex > w.EmittedThroughEdge)
            w.EmittedThroughEdge = w.EdgeIndex;

        var moving = w.Mode == MovementMode.Moving;
        var flags = moving
            ? RpMovementV2Flags.Edge
            : RpMovementV2Flags.WalkEnd;
        if (moving && !w.Route.HasNext)
            flags |= RpMovementV2Flags.FinalEdge;

        // Lookahead: the walker's next REAL route tiles. The cursor already sits
        // past the edge being emitted, so these are genuinely future tiles, not
        // a re-send of this one. Provisional by nature - a redirect supersedes
        // them via RouteRevision.
        var lookahead = System.Array.Empty<LookaheadTile>();
        var lookCount = 0;
        if (moving && w.Route.HasNext)
        {
            var map = room.Room.GetGameMap();
            var max = System.Math.Min(MovementSettings.LookaheadMax, w.Route.Length - w.Route.Cursor);
            if (max > 0 && map != null)
            {
                lookahead = new LookaheadTile[max];
                for (var i = 0; i < max; i++)
                {
                    var tile = w.Route[w.Route.Cursor + i];
                    lookahead[i] = new LookaheadTile(
                        tile.X, tile.Y, MovementEdgeRecord.Z100(map.SqAbsoluteHeight(tile.X, tile.Y)));
                }
                lookCount = max;
            }
        }

        room.Staged.Add(new MovementEdgeRecord(
            w.VirtualId, w.WalkSessionId, w.RouteRevision, w.EdgeIndex, flags,
            MovementSettings.IntervalMs, w.EdgeStartTick(w.EdgeIndex),
            w.Tile.X, w.Tile.Y, MovementEdgeRecord.Z100(w.TileZ),
            w.EdgeTo.X, w.EdgeTo.Y, MovementEdgeRecord.Z100(w.EdgeToZ),
            w.EdgeToZ, w.Facing, lookahead, lookCount));
    }

    /// <summary>
    /// A redirect replaced future geometry: re-emit from the first index the new
    /// route describes. Indexes at or below the elapsing edge are never touched.
    /// </summary>
    private static void StageCorrection(RoomMovement room, MovementState w, int fromEdgeIndex)
    {
        room.HasStagedWork = true;
        room.HasImmediateWork = true;
        if (fromEdgeIndex - 1 > w.EmittedThroughEdge)
            w.EmittedThroughEdge = fromEdgeIndex - 1;
    }

    private static void QueueTileEvents(RoomMovement room, MovementState w, Point left, Point entered)
    {
        MovementWorkQueues.EnqueueTileEvent(room, w, left, entered);
    }
}

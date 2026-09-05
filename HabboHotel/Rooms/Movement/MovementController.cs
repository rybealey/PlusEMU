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

        w.WalkSessionId++;
        w.RouteRevision = 0;
        w.EdgeIndex = 0;
        w.TimelineOrigin = nowMs;
        w.EmittedThroughEdge = -1;
        w.AwaitingEventsThroughEdge = -1;
        w.EventsProcessedThroughEdge = -1;
        w.Target = target;
        w.Tile = tile;
        w.TileZ = tileZ;
        w.Mode = MovementMode.Moving;

        PlanNextEdge(room, w, map, ctx, nowMs, immediate: true);
        return w.Mode == MovementMode.Moving;
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
        if (room.Closed || w.Mode != MovementMode.Moving)
            return;

        var map = room.Room.GetGameMap();
        if (map == null)
        {
            StopWalk(room, w);
            return;
        }

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
            var replanned = AStarPathfinder.FindRoute(
                map, room.Scratch, w.Route, w.Tile, w.Target, ctx,
                baseIndex: w.EdgeIndex, allowPartial: true);
            if (replanned == PathResult.None || !w.Route.HasNext)
            {
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

        // The movement-critical tile barrier (A9): committing ONTO a flagged
        // tile must block the NEXT commit until Q2 has processed its effects.
        if (TileEffects.IsMovementCritical(room, next))
            w.AwaitingEventsThroughEdge = w.EdgeIndex;

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
        w.Mode = MovementMode.Standing;
        w.EdgeTo = w.Tile;
        w.EdgeToZ = w.TileZ;
        w.Route.Clear();
        w.AwaitingEventsThroughEdge = -1;
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
            if (walker.Mode != MovementMode.Moving)
                continue;
            if (room.Walkers.Contains(walker))
                continue;
            MovementCounters.OrphanRecovered();
            room.Walkers.InsertOrUpdate(walker, nowMs);
            walker.Queued = true;
        }
    }

    // ---- staging ----------------------------------------------------------
    // Frame CONTENT (the UserUpdate "mv" entry followed by its 4110 record) is
    // wired at cutover. While V2 is inactive these mark the room as having work
    // so the seal/flush path and its cadence can be exercised and measured.

    private static void StageEdge(RoomMovement room, MovementState w, bool immediate)
    {
        room.HasStagedWork = true;
        if (immediate)
            room.HasImmediateWork = true;
        if (w.EdgeIndex > w.EmittedThroughEdge)
            w.EmittedThroughEdge = w.EdgeIndex;

        // Snapshot the RoomUser effect. Applied by the Q1 worker under
        // _cycleLock - never written from this thread (see RoomMovement.Staged).
        room.Staged.Add(new PendingEdgeCommit(
            w.VirtualId, w.Tile, w.TileZ, w.EdgeTo, w.EdgeToZ, w.Facing,
            w.Mode == MovementMode.Moving));
    }

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

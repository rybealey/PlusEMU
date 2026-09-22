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
            allowPartial: allowPartial);

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
        // LATCHED HERE AND NOWHERE ELSE. Every edge start in this session is
        // TimelineOrigin + k * IntervalMs, so the pace has to be fixed for as
        // long as the timeline it divides. Picking it up at the session
        // boundary is also what lets an escort that began mid-stride take
        // effect on the medic's next walk instead of rewriting the one they
        // are already halfway through.
        w.IntervalMs = w.DesiredIntervalMs;
        w.EmittedThroughEdge = -1;
        w.Target = target;
        w.Tile = tile;
        w.TileZ = tileZ;

        // A NEW SESSION MUST NOT INHERIT THE PREVIOUS WALK'S DEBOUNCE. These
        // two were the only fields StartWalk left carrying state across
        // sessions, so a walk beginning within RepathMinIntervalMs of the last
        // walk's final redirect would silently swallow a redirect to that same
        // tile - a click that simply did nothing.
        //
        // Reset to "the window has just expired", NOT to the field's own
        // long.MinValue initialiser. `nowMs - long.MinValue` overflows in an
        // unchecked context and wraps NEGATIVE, which satisfies the `< interval`
        // test rather than failing it - that sentinel is only harmless today
        // because LastRepathTarget must match as well, and it defaults to 0,0,
        // which is a real tile. Using it here would arm that hazard on every
        // walk instead of once per walker.
        w.LastRepathTarget = target;
        w.LastRepathAtMs = nowMs - MovementSettings.RepathMinIntervalMs;

        MovementCounters.WalkStart();

        if (origin > nowMs)
        {
            // Joining the phase. The route is planned, but NOTHING is emitted
            // until the boundary arrives - see MovementMode.Pending.
            w.Mode = MovementMode.Pending;
            room.Walkers.InsertOrUpdate(w, origin);
            return true;
        }

        w.Mode = MovementMode.Moving;
        PlanNextEdge(room, w, map, ctx, nowMs, immediate: true);
        return w.Mode == MovementMode.Moving;
    }

    /// <summary>
    /// Pick this walk's TimelineOrigin, joining the room's movement phase.
    ///
    /// IT ALWAYS JOINS. The distance to the next boundary is 0..IntervalMs-1,
    /// so there is no longer anything for it to exceed: alignment is guaranteed
    /// rather than opportunistic. The cost is up to one interval of input
    /// latency (~250ms on average) on any walk begun while somebody else is
    /// already walking.
    ///
    /// Trading that back for responsiveness is a real change and not a number -
    /// the ceiling that used to gate it, and the "did not join" case it
    /// selected, were both deleted once the ceiling equalling IntervalMs made
    /// them unreachable. MovementSettings says what restoring them takes.
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
        w.JoinStackedAtRequest = false;

        if (!w.IsRealUser)
        {
            w.LastPhaseDecision = PhaseDecision.None;
            return nowMs;
        }

        var holder = PhaseHolder(room, w);

        if (holder == null)
        {
            room.PhaseAnchor = nowMs;
            w.LastPhaseDecision = PhaseDecision.Established;
            return nowMs;
        }

        // DIAGNOSTIC ONLY, and the whole reason PhaseHolder returns the walker
        // rather than a bool: whether the two were stacked AT THE REQUEST is
        // only knowable here. The holder is moving, so by the time edge 0 is
        // staged an interval later its tile has already changed.
        w.JoinStackedAtRequest = holder.Tile == w.Tile;

        var interval = MovementSettings.IntervalMs;
        var delta = ((room.PhaseAnchor - nowMs) % interval + interval) % interval;

        // ALWAYS JOINS. delta is 0..IntervalMs-1 by construction, so there is no
        // "too far to bother" case to test for - see MovementSettings, where the
        // ceiling that used to gate this lived. delta == 0 needs no branch of
        // its own either: it is already on the boundary, and this returns nowMs
        // with a zero delay, which is exactly that.
        w.LastPhaseDecision = PhaseDecision.Aligned;
        w.LastStartDelayMs = (int)delta;
        return nowMs + delta;
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
    internal static MovementState? PhaseHolder(RoomMovement room, MovementState self)
    {
        foreach (var other in room.States.Values)
        {
            if (ReferenceEquals(other, self) || !other.IsRealUser)
                continue;
            if (other.Mode == MovementMode.Moving || other.Mode == MovementMode.Pending)
                return other;
        }
        return null;
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
            allowPartial: true);

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
    /// <param name="stageCorrection">
    /// Whether to publish the corrected edge early. TRUE for every caller that
    /// redirects a walker MID-EDGE, which is all of them except one.
    ///
    /// FALSE ONLY FROM THE DEFERRED RETRY IN AdvanceWalker, and only because
    /// there is nothing there for an early publish to beat. Early publication
    /// exists to overtake the client's lookahead: the client begins drawing the
    /// next edge the moment its cycleStart passes, without waiting for a
    /// packet, so a mid-edge correction has to arrive before that. At the retry
    /// the next edge has not been advertised at all yet - PlanNextEdge is about
    /// to stage it, moments later, already carrying this new route.
    ///
    /// Publishing anyway put the SAME pair of tiles on the wire twice: once
    /// here as w.EdgeIndex + 1, and once from PlanNextEdge as w.EdgeIndex. The
    /// client walked the step, was told to walk it again, and jumped back to
    /// do so. That is the flicker seen on beta on 2026-09-22.
    /// </param>
    public static bool Redirect(
        RoomMovement room, MovementState w, Point target, in TraverseContext ctx,
        long nowMs, bool allowPartial = true, bool stageCorrection = true)
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

        // 2b. DEFER WHEN THE WALKER IS BEHIND THE ELAPSING INDEX.
        //
        // w.EdgeIndex < e means the server has not yet committed the edge the
        // CLIENT is already rendering: e is derived from the timeline, and the
        // client began that edge from lookahead when its cycleStart passed.
        // SyncCommitsTo cannot always close the gap - it is bounded by
        // w.EdgeIndex < w.EmittedThroughEdge, so a beat that ran late leaves
        // the walker short.
        //
        // Planning anyway would restage index w.EdgeIndex + 1 at the boundary,
        // which in this state is at or before e - the edge in flight. Rewriting
        // the geometry of an edge the client has already begun is the
        // crossing/following hitch, measured directly as a sideways jump under
        // a perfectly correct phase.
        //
        // HISTORICAL NOTE, because this guard used to carry a second
        // justification that no longer applies: it also masked
        // PublishCorrectedEdgeEarly taking its index from the route's label
        // (e + 1) rather than from w.EdgeIndex + 1, which put one pair of tiles
        // on the wire under two indexes and tore a one-tile hole in the chain.
        // That is fixed at its source - the early publish derives its own index
        // now - so this guard stands on the reason above alone.
        //
        // Nothing is planned or published here. The target is kept and retried
        // from AdvanceWalker once the commit path has brought w.EdgeIndex up to
        // the elapsing index.
        if (w.EdgeIndex < e)
        {
            w.DeferredRedirectTarget = target;
            // THE counter for this condition. A second one, redirectBehindElapsing,
            // used to be incremented further down after planning; this guard made
            // it unreachable and it was removed on 2026-09-21. If you are asking
            // how often a redirect lands behind the elapsing index, it is this.
            MovementCounters.RedirectDeferredBehindElapsing();
            return false;
        }

        // 3. Origin = terminal of the CURRENT ELAPSING EDGE.
        //    NOT the last promised terminal: that would force the avatar to
        //    walk to the end of advertised lookahead (up to 1500ms) before
        //    turning, which is precisely the responsiveness bug this rule fixes.
        var origin = w.EdgeTo;

        // 4. Plan from that origin.
        var result = AStarPathfinder.FindRoute(
            map, room.Scratch, w.Route, origin, target, ctx,
            allowPartial: allowPartial);

        if (result == PathResult.None || !w.Route.HasNext)
            return false; // keep walking the existing route

        // MEASUREMENT ONLY, changing nothing. How near the boundary of the
        // edge it is about to restage this replan lands. The client begins
        // that edge from lookahead the instant its cycleStart passes, while
        // this correction is still in flight, so a small margin means the
        // geometry is being rewritten under an edge already being rendered.
        // Counted here rather than earlier so only replans that actually
        // stage are counted - a failed pathfind restages nothing.
        MovementCounters.RedirectMargin(w.EdgeStartTick(e + 1) - nowMs);

        // 5/6/7. Route identity advances; the movement clock does not.
        MovementCounters.Redirect();
        w.DeferredRedirectTarget = null;
        w.RouteRevision++;
        w.Target = target;
        w.LastRepathAtMs = nowMs;
        w.LastRepathTarget = target;
        // UNCHANGED, deliberately: WalkSessionId, TimelineOrigin, EdgeIndex,
        //                          DueTick / queue entry, timing alignment.

        // 8. Future indexes (> e) may be restaged; indexes <= e never change.
        //
        // Skipped from the deferred retry, where the next edge has not been
        // staged yet and the staging about to happen already carries this
        // route - see the stageCorrection parameter. The route swap above
        // still stands either way; only the extra packet is withheld.
        if (stageCorrection)
            StageCorrection(room, w, e + 1, map);
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
    /// and EdgeIndex advances. Emits nothing.
    ///
    /// Tile effects are NOT queued here. They run inline on the outbound thread
    /// in RoomUserManager.ApplyMovementFrame, in order with the commit - see
    /// MovementWorkQueues for why the queue that used to own them is gone.
    /// </summary>
    private static bool CommitEdgeSilently(RoomMovement room, MovementState w, long nowMs)
    {
        if (w.Mode != MovementMode.Moving)
            return false;

        MovementCounters.Commit();
        w.Tile = w.EdgeTo;
        w.TileZ = w.EdgeToZ;
        w.EdgeIndex++;
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
            var startCtx = MovementWalkerContext.For(room.Room, w.VirtualId);
            PlanNextEdge(room, w, map, startCtx, nowMs, immediate: true);
            return;
        }

        MovementCounters.Advance();
        var lateMs = nowMs - scheduledTick;
        if (lateMs > w.IntervalMs)
            MovementCounters.BeatLate(lateMs);

        // (a) commit the edge that just finished
        CommitEdgeSilently(room, w, nowMs);

        // (b) lateness: honour promises, never contradict them.
        if (lateMs > w.IntervalMs)
        {
            var elapsing = w.ElapsingEdgeIndex(nowMs);
            SyncCommitsTo(room, w, elapsing, nowMs);
        }

        // (b2) a redirect deferred because the walker was behind the elapsing
        // index. Retried HERE because the commit above is the only thing that
        // brings w.EdgeIndex forward, and only while the two indexes now agree
        // - Redirect would otherwise simply defer it again.
        //
        // The flag is cleared BEFORE the attempt on purpose: a retry that fails
        // for any other reason (no route, debounce) drops the click exactly as
        // an ordinary redirect would, rather than re-arming itself every beat
        // against a target that may never be reachable.
        if (w.DeferredRedirectTarget is { } deferredTarget && w.Mode == MovementMode.Moving)
        {
            w.DeferredRedirectTarget = null;

            if (w.EdgeIndex == w.ElapsingEdgeIndex(nowMs))
            {
                var deferredCtx = MovementWalkerContext.For(room.Room, w.VirtualId);

                // NO EARLY PUBLISH FROM HERE. CommitEdgeSilently has just set
                // Tile = EdgeTo, so the walker is standing still between edges
                // and this plans from exactly where it is - the route swap is
                // correct. What would NOT be correct is the correction packet:
                // PlanNextEdge runs a few lines below and stages this same
                // geometry as w.EdgeIndex, so publishing it here as
                // w.EdgeIndex + 1 puts one pair of tiles on the wire under two
                // indexes, and the avatar walks the step then jumps back to
                // walk it again.
                if (Redirect(room, w, deferredTarget, deferredCtx, nowMs, stageCorrection: false))
                    MovementCounters.RedirectDeferredRecovered();
            }
        }

        // (c) plan the next edge
        // Re-asked every beat rather than carried from the request, so somebody
        // who clocks OFF mid-walk is stopped at the gate instead of coasting
        // through on a permission they no longer hold. That now covers ALL of
        // the walker's permissions, not just duty: :override and riding used to
        // be rebuilt as false here, so they survived exactly one tile.
        var ctx = MovementWalkerContext.For(room.Room, w.VirtualId);
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
            // ONCE AN EDGE HAS BEGUN, ITS GEOMETRY IS IMMUTABLE.
            //
            // This re-plan stages from w.EdgeIndex and bumps RouteRevision.
            // That index is normally still in the future: CommitEdgeSilently
            // advanced it moments ago and StageEdge emits it with an absolute
            // EdgeStartTick derived from TimelineOrigin. But the start tick is
            // wall-clock, not "now" - so when this beat runs LATE (the case
            // already counted by BeatLate above), that index has ALREADY
            // STARTED on the client, which has been rendering it since its
            // cycleStart from the lookahead previously advertised for it.
            //
            // Re-planning then emits the SAME edge index at a higher revision
            // with different from/to, and the client - correctly trusting the
            // newer revision - swaps geometry underneath an avatar that is
            // part-way along the tile. That is a teleport, and it was observed
            // as exactly that: index 34, revision 14 -> 15, 12,3->13,3
            // replaced by 12,3->11,4 at phase 0.023.
            //
            // So the elapsing index is derived from the authoritative timeline
            // and compared against the index about to be staged. Edge e
            // finishes on the geometry already promised; the new route begins
            // at e + 1, planned on the next beat when that index is genuinely
            // fresh. Nothing about timing, alignment or the interval changes -
            // only WHICH index a re-plan is allowed to rewrite.
            var elapsing = w.ElapsingEdgeIndex(nowMs);

            if (w.EdgeIndex <= elapsing)
            {
                // Already begun. Honour what was advertised for this index and
                // stage it unchanged; the block is re-evaluated next beat from
                // a fresh index. One edge completes onto a tile that has since
                // become impassable, which is a race stock Habbo loses too -
                // and is bounded, unlike rewriting a moving avatar's position.
                MovementCounters.ReplanDeferred();
            }
            else
            {
                // Not yet started: rewriting this index is legal, because the
                // client cannot have begun rendering an edge whose cycleStart
                // is still in the future.
                MovementCounters.Replan();
                var replanned = AStarPathfinder.FindRoute(
                    map, room.Scratch, w.Route, w.Tile, w.Target, ctx,
                    allowPartial: true);
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
        }

        w.Route.Advance();
        w.EdgeTo = next;
        w.EdgeToZ = map.SqAbsoluteHeight(next.X, next.Y);
        w.Facing = (byte)Rotation.Calculate(w.Tile.X, w.Tile.Y, next.X, next.Y);

        // No tile-event barrier is armed here, and there is no longer one to
        // arm: tile effects run inline with the commit on the outbound thread,
        // so there is nothing for a walker to wait on. If they are ever moved
        // back onto a queue, the barrier has to come back with them - see
        // MovementWorkQueues.

        StageEdge(room, w, immediate);

        var nextDue = w.EdgeStartTick(w.EdgeIndex) + w.IntervalMs;
        room.Walkers.InsertOrUpdate(w, nextDue); // never a bare Push (I-1)
    }

    public static void StopWalk(RoomMovement room, MovementState w)
    {
        if (w.Queued)
            room.Walkers.Remove(w);

        // A walker abandoned while still Pending never put anything on the wire,
        // so there is nothing to close off. A walk-end here would tell the client
        // to forget a unit it was never told about.
        var neverEmitted = w.Mode == MovementMode.Pending && w.EmittedThroughEdge < 0;

        w.Mode = MovementMode.Standing;
        w.EdgeTo = w.Tile;
        w.EdgeToZ = w.TileZ;
        w.DeferredRedirectTarget = null;
        w.Route.Clear();

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
        // TEMPORARY DIAGNOSTIC, edge 0 only - see RpMovementV2Flags.
        if (w.EdgeIndex == 0 && w.JoinStackedAtRequest)
            flags |= RpMovementV2Flags.JoinStackedAtRequest;

        // Lookahead: the walker's next REAL route tiles. The cursor already sits
        // past the edge being emitted, so these are genuinely future tiles, not
        // a re-send of this one. Provisional by nature - a redirect supersedes
        // them via RouteRevision.
        var lookahead = System.Array.Empty<LookaheadTile>();
        var lookCount = 0;
        var map = room.Room.GetGameMap();
        if (moving && w.Route.HasNext)
        {
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
            w.IntervalMs, w.EdgeStartTick(w.EdgeIndex),
            w.Tile.X, w.Tile.Y, MovementEdgeRecord.Z100(w.TileZ),
            w.EdgeTo.X, w.EdgeTo.Y, MovementEdgeRecord.Z100(w.EdgeToZ),
            w.EdgeToZ, w.Facing, lookahead, lookCount, w.LastStartDelayMs));

        // pixelrp police escort: the captor's shadow rides in the same frame.
        StageShadow(room, w, map, moving, flags);
    }

    // ---- escort shadow (pixelrp police) -----------------------------------
    // A suspect being escorted is not a walker. They have no route and never
    // will while shadowed: for every record the captor stages, one is staged
    // for them with IDENTICAL identity and timing and geometry one tile in front
    // of the captor's. That is what "dragged along like an image" costs - the
    // pathfinder is simply not consulted for them. Everything here is state
    // mutation, map reads and staging: exactly what the scheduler thread is
    // allowed to do (MovementSchedulerGuard, invariant I-5).

    /// <summary>The (dx, dy) of one step in a facing - the inverse of Rotation.Calculate.</summary>
    internal static Point FacingDelta(byte facing) => facing switch
    {
        0 => new Point(0, -1),
        1 => new Point(1, -1),
        2 => new Point(1, 0),
        3 => new Point(1, 1),
        4 => new Point(0, 1),
        5 => new Point(-1, 1),
        6 => new Point(-1, 0),
        7 => new Point(-1, -1),
        _ => new Point(0, 0),
    };

    /// <summary>
    /// The tile one step in front of <paramref name="from"/> in <paramref name="facing"/>
    /// when a unit could walk onto it mid-route, else <paramref name="from"/>
    /// itself. The fallback is what happens when the captor walks face-first up
    /// to a wall or a desk: the suspect shares their tile for that beat rather
    /// than being put on something nobody can stand on, and reclaims the lead
    /// the moment there is somewhere to lead to. Evaluated as a NON-final step
    /// on purpose: tiles that are legal only as a route's last tile (the door)
    /// are not places to park somebody who did not choose to go there.
    /// Occupancy is deliberately not consulted - this hotel lets players share
    /// tiles (I-10).
    /// </summary>
    /// <summary>
    /// The tile a shadow sits on relative to its captor: one step along the
    /// facing when it leads, one step against it when it trails.
    ///
    /// Reversing the FACING rather than negating the delta keeps this one
    /// question with one answer - FrontTile owns the bounds check and the
    /// traversability check, and a trailing shadow needs both just as much.
    /// Facings run 0-7 clockwise, so the opposite of any of them is +4 mod 8.
    /// </summary>
    public static Point ShadowTile(Gamemap map, Point from, byte facing, bool behind) =>
        FrontTile(map, from, behind ? (byte)((facing + 4) % 8) : facing);

    public static Point FrontTile(Gamemap map, Point from, byte facing)
    {
        var d = FacingDelta(facing);
        if (map == null || (d.X == 0 && d.Y == 0))
            return from;
        var tile = new Point(from.X + d.X, from.Y + d.Y);
        if (!CanTraverse.InBounds(map, tile.X, tile.Y))
            return from;
        // No onDuty here, so corp gates do not apply. Deliberate: this is a
        // geometry question about a tile in front of a captor, asked with no
        // walker in hand to have a rota. The escorted body is being PUT there,
        // not walking in.
        var ctx = new TraverseContext(cornerPolicy: CornerPolicy.Off);
        return CanTraverse.IsPassable(CanTraverse.Evaluate(map, from, tile, isFinalStep: false, ctx), false) ? tile : from;
    }

    /// <summary>
    /// Stage the shadow's record for the captor record just staged. Same
    /// session, revision, edge index and start tick, so both units switch
    /// edges on the same client frame. The shadow's edge runs from wherever
    /// its previous edge ended to the tile in front of the captor's new
    /// destination: on a straight that is one tile like the captor's; on a
    /// bend the front tile swings with the facing and the suspect slides to it
    /// in the same beat, which is exactly what the Arcturus original produced
    /// (setLocation to "to + dir" with the client tweening from wherever the
    /// suspect was). Every shadow edge is therefore contiguous with the last -
    /// no jumps for the client to paper over.
    ///
    /// Staging is strictly one record per edge per beat (Redirect restages
    /// nothing; the next beat's PlanNextEdge carries the new revision), so
    /// "where the previous edge ended" is always the shadow's own EdgeTo.
    /// </summary>
    private static void StageShadow(RoomMovement room, MovementState w, Gamemap? map, bool moving, int flags)
    {
        if (w.ShadowVirtualId == MovementState.NoShadow)
            return;
        if (!room.States.TryGetValue(w.ShadowVirtualId, out var s) || s.ShadowedBy != w.VirtualId)
        {
            // The suspect has gone (or been unpaired from their side); heal the link.
            w.ShadowVirtualId = MovementState.NoShadow;
            return;
        }
        if (map == null)
            return;

        Point from, to;
        if (moving)
        {
            from = s.EdgeTo;
            to = ShadowTile(map, w.EdgeTo, w.Facing, w.ShadowBehind);
            s.Facing = w.Facing;
            if (from == to)
            {
                // The shadow's next tile is the one it already rests on -
                // the captor is stepping onto it and the tile past is
                // blocked, or (trailing) the captor has not left the tile
                // behind them. There is nothing to walk. Rest for the
                // beat rather than animate a walk to nowhere; the next real
                // edge picks the unit up again in the same session.
                moving = false;
                flags = RpMovementV2Flags.WalkEnd;
            }
        }
        else
        {
            // Walk end: the suspect comes to rest on their side of wherever
            // the captor actually stopped. For a walk that ran its course that is
            // the tile the last shadow edge was already heading to; for one
            // halted mid-step (a stun, a block) the captor snaps back to Tile
            // and the suspect is put in front of THAT rather than left two
            // tiles ahead.
            from = ShadowTile(map, w.Tile, w.Facing, w.ShadowBehind);
            to = from;
            s.Facing = w.Facing;
        }
        var fromZ = map.SqAbsoluteHeight(from.X, from.Y);
        var toZ = map.SqAbsoluteHeight(to.X, to.Y);
        s.Tile = from;
        s.TileZ = fromZ;
        s.EdgeTo = to;
        s.EdgeToZ = toZ;
        // Mirror the wire identity onto the shadow's state so a later close-out
        // (Unpair) can be written in the same session the client is holding.
        s.WalkSessionId = System.Math.Max(s.WalkSessionId, w.WalkSessionId);
        s.RouteRevision = w.RouteRevision;
        s.EdgeIndex = w.EdgeIndex;
        s.TimelineOrigin = w.TimelineOrigin;
        // The pace too, or the close-out Unpair stages from s.EdgeStartTick
        // would be timed on a 500ms grid the captor's edges never used.
        s.IntervalMs = w.IntervalMs;
        s.Mode = MovementMode.Standing;

        var lookahead = System.Array.Empty<LookaheadTile>();
        var lookCount = 0;
        if (moving && w.Route.HasNext)
        {
            var max = System.Math.Min(MovementSettings.LookaheadMax, w.Route.Length - w.Route.Cursor);
            if (max > 0)
            {
                // The same provisional chain the captor advertises, each tile
                // pushed one step ahead along the direction it is entered in.
                // Provisional exactly as the captor's is: a redirect supersedes
                // both units' chains in the same beat via RouteRevision.
                lookahead = new LookaheadTile[max];
                var prev = w.EdgeTo;
                for (var i = 0; i < max; i++)
                {
                    var tile = w.Route[w.Route.Cursor + i];
                    var f = (byte)Rotation.Calculate(prev.X, prev.Y, tile.X, tile.Y);
                    var ahead = ShadowTile(map, tile, f, w.ShadowBehind);
                    lookahead[i] = new LookaheadTile(ahead.X, ahead.Y, MovementEdgeRecord.Z100(map.SqAbsoluteHeight(ahead.X, ahead.Y)));
                    prev = tile;
                }
                lookCount = max;
            }
        }

        room.Staged.Add(new MovementEdgeRecord(
            s.VirtualId, w.WalkSessionId, w.RouteRevision, w.EdgeIndex, flags,
            w.IntervalMs, w.EdgeStartTick(w.EdgeIndex),
            from.X, from.Y, MovementEdgeRecord.Z100(fromZ),
            to.X, to.Y, MovementEdgeRecord.Z100(toZ),
            toZ, s.Facing, lookahead, lookCount, w.LastStartDelayMs));
    }

    /// <summary>
    /// Put a unit on a tile, facing a way, as a hard reset the client jumps to
    /// at once. This is the first thing on this server to produce the
    /// Displacement flag; the client has always handled it (it drops the unit's
    /// timed state and takes the next UserUpdate at face value). Used to seat a
    /// suspect in front of their captor when the escort begins - and only then;
    /// a captor turning on the spot no longer moves them. Caller holds
    /// MovementLock.
    /// </summary>
    public static void StageDisplacement(RoomMovement room, MovementState s, Point tile, byte facing, Gamemap? map, long nowMs)
    {
        if (s.Mode == MovementMode.Moving || s.Mode == MovementMode.Pending)
            StopWalk(room, s);
        if (s.Queued)
            room.Walkers.Remove(s);
        s.WalkSessionId++; // "++ on every displacement" - the field's own contract
        s.RouteRevision = 0;
        s.EdgeIndex = 0;
        s.Mode = MovementMode.Standing;
        s.Route.Clear();
        s.Tile = tile;
        s.EdgeTo = tile;
        s.TileZ = map != null ? map.SqAbsoluteHeight(tile.X, tile.Y) : s.TileZ;
        s.EdgeToZ = s.TileZ;
        s.Facing = facing;
        s.EmittedThroughEdge = -1;
        var z100 = MovementEdgeRecord.Z100(s.TileZ);
        room.Staged.Add(new MovementEdgeRecord(
            s.VirtualId, s.WalkSessionId, 0, 0, RpMovementV2Flags.Displacement,
            s.IntervalMs, nowMs,
            tile.X, tile.Y, z100, tile.X, tile.Y, z100, s.TileZ, facing,
            System.Array.Empty<LookaheadTile>(), 0));
        room.HasStagedWork = true;
        room.HasImmediateWork = true;
    }

    /// <summary>
    /// Close out a shadow that is being released: a walk-end in the session
    /// the client is holding for it, resting on the tile its last edge ended
    /// on. Without this a suspect let go mid-walk keeps the walking posture
    /// server-side and the client's unit starves rather than being forgotten.
    /// Caller holds MovementLock.
    /// </summary>
    public static void StageShadowEnd(RoomMovement room, MovementState s, Gamemap? map)
    {
        s.Tile = s.EdgeTo;
        s.TileZ = map != null ? map.SqAbsoluteHeight(s.Tile.X, s.Tile.Y) : s.EdgeToZ;
        s.EdgeToZ = s.TileZ;
        s.Mode = MovementMode.Standing;
        s.Route.Clear();
        var z100 = MovementEdgeRecord.Z100(s.TileZ);
        room.Staged.Add(new MovementEdgeRecord(
            s.VirtualId, s.WalkSessionId, s.RouteRevision, s.EdgeIndex + 1, RpMovementV2Flags.WalkEnd,
            s.IntervalMs, s.EdgeStartTick(s.EdgeIndex + 1),
            s.Tile.X, s.Tile.Y, z100, s.Tile.X, s.Tile.Y, z100, s.TileZ, s.Facing,
            System.Array.Empty<LookaheadTile>(), 0));
        room.HasStagedWork = true;
        room.HasImmediateWork = true;
    }

    /// <summary>
    /// A redirect replaced future geometry: re-emit from the first index the new
    /// route describes. Indexes at or below the elapsing edge are never touched.
    /// </summary>
    private static void StageCorrection(RoomMovement room, MovementState w, int fromEdgeIndex, Gamemap? map)
    {
        room.HasStagedWork = true;
        room.HasImmediateWork = true;

        // Asserts everything below the corrected index as "committable", which
        // may include indexes no record was ever individually staged for. That
        // is what lets SyncCommitsTo reach the elapsing edge on a later beat -
        // it bounds on this value. See MovementState.EmittedThroughEdge: the
        // field is that bound, not a transmission log, and it deliberately does
        // NOT account for the lookahead the client is already rendering from.
        if (fromEdgeIndex - 1 > w.EmittedThroughEdge)
            w.EmittedThroughEdge = fromEdgeIndex - 1;

        PublishCorrectedEdgeEarly(room, w, map);
    }

    /// <summary>
    /// EXPERIMENT: transmit the corrected first index as soon as the redirect
    /// decides it, instead of waiting for the beat that stages it.
    ///
    /// THE PROBLEM THIS TESTS. StageCorrection sets HasImmediateWork and the
    /// scheduler's immediate flush duly fires - but room.Staged holds nothing
    /// for the corrected index, because that record is not built until the next
    /// beat runs PlanNextEdge into StageEdge. That beat is queued at
    /// EdgeStartTick(EdgeIndex) + IntervalMs, which IS this index's own
    /// cycleStart. So the correction reaches the wire at the very moment the
    /// client begins rendering the edge from lookahead, every time. The
    /// immediate-flush path already exists and already runs on every redirect;
    /// it simply has an empty payload. This gives it one.
    ///
    /// PUBLICATION TIMING ONLY. Nothing else moves: not TimelineOrigin, not
    /// WalkSessionId, not the RouteRevision rules, not the 500ms interval, not
    /// EmittedThroughEdge's meaning, not the lookahead policy, not the packet
    /// format. The boundary is still e + 1 exactly as before, so there is no
    /// added latency - a redirect still takes effect on the next edge.
    ///
    /// The record is PublishOnly: room.Staged is ALSO the server-truth commit
    /// path, and committing a future edge early would move the avatar a tile
    /// ahead and run its tile effects early. See MovementEdgeRecord.PublishOnly.
    ///
    /// Lookahead is deliberately EMPTY. Only the corrected index is
    /// republished; e + 2 and later keep coming from the normal pipeline, which
    /// refills lookahead when it stages this index again at the boundary.
    /// </summary>
    private static void PublishCorrectedEdgeEarly(
        RoomMovement room, MovementState w, Gamemap? map)
    {
        if (map == null || !w.Route.HasNext || w.Mode != MovementMode.Moving)
            return;

        // THE INDEX COMES FROM THE COUNTER THAT GOVERNS STAGING, NEVER FROM THE
        // ROUTE'S LABEL, and it is derived here rather than passed in so it
        // cannot be supplied wrongly.
        //
        // StageEdge always labels its record w.EdgeIndex, so the boundary
        // record for the geometry below will be w.EdgeIndex + 1. This record
        // describes that same geometry, so it must carry that same index.
        //
        // It used to take StageCorrection's fromEdgeIndex, which is e + 1 -
        // derived from the TIMELINE, not from the counter. Those agree only
        // when w.EdgeIndex == e. When they did not, this method published
        // `w.EdgeTo -> PeekNext()` as index e + 1 while the boundary beat
        // published the identical geometry as w.EdgeIndex + 1: two indexes, one
        // pair of tiles, and a one-tile hole between edge n's To and edge
        // n + 1's From. That was the full-tile teleport.
        //
        // The deferral in Redirect now guarantees w.EdgeIndex == e before this
        // runs, so today this is the same number. It is derived anyway, because
        // the correctness of this record should not rest on a guard three
        // hundred lines away continuing to hold.
        //
        // Geometry agrees by construction: `from` is w.EdgeTo, the terminal of
        // edge w.EdgeIndex, which IS where edge w.EdgeIndex + 1 begins.
        var index = w.EdgeIndex + 1;

        // A captor's edges ride with a matching shadow record (StageShadow).
        // Publishing the captor's alone would break that lockstep.
        if (w.ShadowVirtualId != MovementState.NoShadow)
        {
            MovementCounters.CorrectionEPlus1Escort();
            return;
        }

        // FRESH clock, and this is the whole point of the check. Re-deriving
        // with the nowMs that produced e is vacuous - e + 1 > e by
        // construction. The pathfind runs between those two points and can
        // cross a boundary; if it has, this index is already elapsing and must
        // not be rewritten.
        var now = MovementScheduler.Instance.Clock.NowMs;
        if (w.ElapsingEdgeIndex(now) >= index)
        {
            MovementCounters.CorrectionEPlus1NotFuture();
            return;
        }

        var identity = new EdgeIdentity(w.WalkSessionId, w.RouteRevision, index);

        if (w.LastEarlyPublish == identity)
        {
            MovementCounters.CorrectionEPlus1AlreadyStaged();
            return;
        }

        // Geometry of the corrected index, read WITHOUT mutating the walker: it
        // leaves the terminal of the elapsing edge and arrives on the new
        // route's first tile. The cursor is NOT advanced here - the boundary
        // beat still does that, and still stages the committing record.
        var from = w.EdgeTo;
        var to = w.Route.PeekNext();
        var toZ = map.SqAbsoluteHeight(to.X, to.Y);

        var flags = RpMovementV2Flags.Edge;
        if (w.Route.Length - w.Route.Cursor <= 1)
            flags |= RpMovementV2Flags.FinalEdge;

        // TEMPORARY DIAGNOSTIC - see RpMovementV2Flags.ForcedRedirect. THIS
        // record is the one worth marking, of the two this redirect produces:
        // it is the packet that races the client's lookahead, and the boundary
        // beat's own record arrives afterwards, describing a rewrite the client
        // has already been told about. Marking both would double-count a single
        // forced redirect in the browser log.
        var forcedMarginMs = 0;
        if (w.ForcedRedirectMarginMs != MovementState.NotForced)
        {
            flags |= RpMovementV2Flags.ForcedRedirect;
            forcedMarginMs = w.ForcedRedirectMarginMs;
        }

        room.Staged.Add(new MovementEdgeRecord(
            w.VirtualId, w.WalkSessionId, w.RouteRevision, index, flags,
            w.IntervalMs, w.EdgeStartTick(index),
            from.X, from.Y, MovementEdgeRecord.Z100(w.EdgeToZ),
            to.X, to.Y, MovementEdgeRecord.Z100(toZ),
            toZ, (byte)Rotation.Calculate(from.X, from.Y, to.X, to.Y),
            System.Array.Empty<LookaheadTile>(), 0, forcedMarginMs, publishOnly: true));

        w.LastEarlyPublish = identity;

        MovementCounters.CorrectionEPlus1ImmediateStaged();
    }

}

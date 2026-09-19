using System.Drawing;
using NLog;

namespace Plus.HabboHotel.Rooms.Movement;

/// <summary>
/// pixelrp Movement V2: log every route revision that restages an edge near the
/// active one. DIAGNOSTIC ONLY - it reads state and writes log lines, and
/// changes no movement behaviour at all.
///
/// IT EXISTS TO SETTLE ONE QUESTION, and only that one. The crossing/following
/// hitch was measured (edge 103 rewritten 8,16-&gt;8,17 into 8,16-&gt;7,15 at
/// revision 41, phase 0.128) but the CAUSE has two candidates the earlier
/// forensics could not tell apart:
///
///   A) the server rewrites the edge that is ACTUALLY ACTIVE - firstIndex &lt;= e
///   B) the server rewrites e + 1 while it is still genuinely future
///      server-side, but the 4110 leaves so close to that edge's own cycleStart
///      that the client has already begun rendering it from lookahead
///
/// A needs a server fix. B cannot be fixed on the server at all - no packet can
/// arrive before it was sent - so knowing which one it is decides where the next
/// attempt goes. The reverted fix (emulator cce7b5a1 / client b12d7655) assumed
/// B and shipped BOTH halves at once, which is why it could not be attributed
/// when beta got worse.
///
/// WHY A LINE IS WRITTEN AT SEND TIME AND NOT AT CAPTURE TIME. The whole
/// discriminator is the 4110's actual send timestamp against the restaged
/// edge's own cycleStart, and that is not known when the revision is planned.
/// So a revision is held in a small pending ring and emitted once its packet
/// goes out - one correlated line rather than two the reader has to join up.
/// A revision whose index never goes out is swept and emitted as NOSEND, so
/// nothing is silently dropped.
///
/// LOGGING FROM THE MOVEMENT PATH IS SAFE HERE, and this is deliberate rather
/// than lucky: Config/nlog.config wraps the console target in an AsyncWrapper
/// with overflowAction="Discard", so a write hands off to a background thread
/// and never blocks the caller. That is what keeps this clear of invariant I-5's
/// ban on a blocking log sink (see MovementSchedulerGuard). Two consequences
/// worth knowing:
///   - lines are written at Info, because nlog.config's rule is minlevel="Info"
///   - under a burst the wrapper DISCARDS rather than stalls, so a very busy
///     hotel can drop lines. `seen` counts every captured revision, so a gap
///     between `seen` and the lines in the log is detectable rather than silent.
///
/// Off by default: disarmed, every hook costs one volatile read.
/// </summary>
public static class MovementReplanTrace
{
    private static readonly Logger Log = LogManager.GetLogger("MovementV2");

    /// <summary>Revisions held awaiting their packet. One per click, so this is ample.</summary>
    public const int Capacity = 96;

    /// <summary>
    /// A held revision whose index has not gone out within this long is emitted
    /// as NOSEND and evicted. Three edges' worth: long enough that a normal
    /// send is never mislabelled, short enough that the ring cannot fill.
    /// </summary>
    public const long StaleMs = 1500;

    /// <summary>
    /// How near an edge's own start a 4110 may leave before the client is
    /// LIKELY to have begun rendering it from lookahead already.
    ///
    /// THIS IS A JUDGEMENT, NOT A MEASUREMENT. The server cannot see receipt
    /// time, so a send margin above zero only means the packet left before the
    /// edge was due to start - it may still land after. 120ms is a generous
    /// allowance for flight plus client frame quantisation; treat B_RISK as
    /// "consistent with B", never as proof of it. B_LATE needs no such
    /// allowance: the packet left AFTER the edge's own cycleStart, so the
    /// client had certainly started it.
    /// </summary>
    public const long FlightAllowanceMs = 120;

    /// <summary>Where a revision came from. The two sites that bump RouteRevision.</summary>
    public enum Origin : byte
    {
        /// <summary>MovementController.Redirect - a click while already moving.</summary>
        Redirect = 0,

        /// <summary>MovementController.PlanNextEdge - the next tile became impassable.</summary>
        BlockedReplan = 1
    }

    private struct Record
    {
        public bool InUse;

        public int VirtualId;
        public long WalkSessionId;
        public long ServerNowMs;
        public long TimelineOrigin;

        /// <summary>The elapsing index derived from the timeline at capture time.</summary>
        public int ActiveEdge;

        /// <summary>The index the walker had in flight, and its geometry.</summary>
        public int CurrentEdgeIndex;
        public int CurFromX, CurFromY, CurToX, CurToY;

        public int NewRevision;

        /// <summary>The first edge index this revision replaces.</summary>
        public int FirstIndex;

        /// <summary>Absolute start tick of <see cref="FirstIndex"/> on this timeline.</summary>
        public long FirstIndexStartTick;

        public bool HasOld;
        public int OldFromX, OldFromY, OldToX, OldToY;
        public int NewFromX, NewFromY, NewToX, NewToY;

        public Origin From;

        /// <summary>Movement-clock tick at which the 4110 for FirstIndex went out; 0 = never.</summary>
        public long SentAtMs;

        /// <summary>FirstIndex - ActiveEdge. Zero or less is hypothesis A by definition.</summary>
        public readonly int Offset => FirstIndex - ActiveEdge;

        /// <summary>Did the geometry of FirstIndex actually change? One that did not is harmless.</summary>
        public readonly bool GeometryChanged =>
            HasOld && (OldFromX != NewFromX || OldFromY != NewFromY || OldToX != NewToX || OldToY != NewToY);

        /// <summary>Margin between send and the edge's own start. Negative = sent after it began.</summary>
        public readonly long SendMarginMs => FirstIndexStartTick - SentAtMs;
    }

    private static readonly Record[] Pending = new Record[Capacity];
    private static readonly object Gate = new();

    private static volatile bool _enabled;

    /// <summary>-1 traces every unit; otherwise only this virtual id.</summary>
    private static volatile int _unitFilter = -1;

    private static long _seen;
    private static long _emitted;
    private static long _dropped;

    public static bool Enabled => _enabled;

    public static int UnitFilter => _unitFilter;

    public static long Seen => Interlocked.Read(ref _seen);

    public static void Arm(int unitFilter)
    {
        lock (Gate)
        {
            Array.Clear(Pending, 0, Capacity);
            Interlocked.Exchange(ref _seen, 0);
            Interlocked.Exchange(ref _emitted, 0);
            Interlocked.Exchange(ref _dropped, 0);
            _unitFilter = unitFilter;
        }
        _enabled = true;
        Log.Info($"[MV2/replan] ARMED unit={(unitFilter < 0 ? "all" : unitFilter.ToString())} " +
                 $"flightAllowanceMs={FlightAllowanceMs} staleMs={StaleMs}");
    }

    /// <summary>Emit everything still held, then stop. Called off the movement path.</summary>
    public static void Disarm()
    {
        Flush();
        _enabled = false;
        lock (Gate)
        {
            Array.Clear(Pending, 0, Capacity);
            _unitFilter = -1;
        }
        Log.Info($"[MV2/replan] DISARMED {Stats()}");
    }

    public static string Stats() =>
        $"seen={Interlocked.Read(ref _seen)} emitted={Interlocked.Read(ref _emitted)} " +
        $"droppedNoSlot={Interlocked.Read(ref _dropped)}";

    /// <summary>
    /// Read the geometry a route currently describes for one absolute edge index.
    ///
    /// MUST be called BEFORE the pathfinder overwrites the buffer, which is why
    /// the Redirect site captures into locals up front.
    ///
    /// RouteBuffer is start-first: Tiles[i] is the DESTINATION of edge
    /// (BaseIndex + i), so edge k arrives at Tiles[k - BaseIndex] and leaves
    /// from Tiles[k - 1 - BaseIndex]. When k is the buffer's own first index
    /// there is no previous tile in it and the edge leaves from wherever the
    /// route was planned, which the caller supplies as <paramref name="planOrigin"/>.
    /// </summary>
    public static bool ReadEdgeGeometry(MovementState w, int edgeIndex, Point planOrigin, out Point from, out Point to)
    {
        from = planOrigin;
        to = planOrigin;

        var route = w.Route;
        var slot = edgeIndex - route.BaseIndex;
        if (slot < 0 || slot >= route.Length)
            return false;

        to = route[slot];
        from = slot >= 1 ? route[slot - 1] : planOrigin;
        return true;
    }

    /// <summary>
    /// Hold one revision until its packet goes out. Caller holds MovementLock
    /// and has already bumped RouteRevision. Stores primitives only - no
    /// formatting and no I/O on this path.
    /// </summary>
    public static void OnRevision(
        MovementState w, Origin origin, long nowMs, int activeEdge, int firstIndex,
        bool hasOld, Point oldFrom, Point oldTo, Point newFrom, Point newTo)
    {
        if (!_enabled)
            return;

        var filter = _unitFilter;
        if (filter >= 0 && w.VirtualId != filter)
            return;

        Interlocked.Increment(ref _seen);

        lock (Gate)
        {
            if (!_enabled)
                return;

            var slot = -1;
            for (var i = 0; i < Capacity; i++)
            {
                if (Pending[i].InUse) continue;
                slot = i;
                break;
            }

            if (slot < 0)
            {
                // Every slot held. Counted rather than silently discarded, and
                // never emitted from here - this is the scheduler thread and the
                // sweep on the outbound path will clear the backlog shortly.
                Interlocked.Increment(ref _dropped);
                return;
            }

            ref var r = ref Pending[slot];
            r.InUse = true;
            r.VirtualId = w.VirtualId;
            r.WalkSessionId = w.WalkSessionId;
            r.ServerNowMs = nowMs;
            r.TimelineOrigin = w.TimelineOrigin;
            r.ActiveEdge = activeEdge;
            r.CurrentEdgeIndex = w.EdgeIndex;
            r.CurFromX = w.Tile.X;
            r.CurFromY = w.Tile.Y;
            r.CurToX = w.EdgeTo.X;
            r.CurToY = w.EdgeTo.Y;
            r.NewRevision = w.RouteRevision;
            r.FirstIndex = firstIndex;
            r.FirstIndexStartTick = w.EdgeStartTick(firstIndex);
            r.HasOld = hasOld;
            r.OldFromX = oldFrom.X;
            r.OldFromY = oldFrom.Y;
            r.OldToX = oldTo.X;
            r.OldToY = oldTo.Y;
            r.NewFromX = newFrom.X;
            r.NewFromY = newFrom.Y;
            r.NewToX = newTo.X;
            r.NewToY = newTo.Y;
            r.From = origin;
            r.SentAtMs = 0;
        }
    }

    /// <summary>
    /// Stamp and emit when the 4110 carrying a revised first index actually goes
    /// out. Called from the room's outbound path, right where MovementTrace
    /// reports, so the timestamp is the one the wire saw.
    ///
    /// Matched on the full edge identity (unit, session, revision, index), which
    /// is a total order in V2, so a stamp can never land on the wrong record.
    /// Also sweeps stale holds, which is what guarantees every captured revision
    /// reaches the log exactly once.
    ///
    /// Formatting and the log call happen OUTSIDE the lock.
    /// </summary>
    public static void OnEdgeSent(in MovementEdgeRecord edge, long serverNowMs)
    {
        if (!_enabled)
            return;

        Record matched = default;
        var hasMatch = false;
        Record[]? stale = null;
        var staleCount = 0;

        lock (Gate)
        {
            if (!_enabled)
                return;

            for (var i = 0; i < Capacity; i++)
            {
                ref var r = ref Pending[i];
                if (!r.InUse)
                    continue;

                if (!hasMatch &&
                    r.VirtualId == edge.VirtualId &&
                    r.WalkSessionId == edge.WalkSessionId &&
                    r.NewRevision == edge.RouteRevision &&
                    r.FirstIndex == edge.EdgeIndex)
                {
                    r.SentAtMs = serverNowMs;
                    matched = r;
                    hasMatch = true;
                    r.InUse = false;
                    continue;
                }

                if (serverNowMs - r.ServerNowMs > StaleMs)
                {
                    stale ??= new Record[Capacity];
                    stale[staleCount++] = r;
                    r.InUse = false;
                }
            }
        }

        if (hasMatch)
            Emit(matched);

        for (var i = 0; i < staleCount; i++)
            Emit(stale![i]);
    }

    /// <summary>Emit everything currently held, regardless of age. Off the movement path.</summary>
    public static void Flush()
    {
        Record[] taken;
        int count;

        lock (Gate)
        {
            taken = new Record[Capacity];
            count = 0;
            for (var i = 0; i < Capacity; i++)
            {
                if (!Pending[i].InUse)
                    continue;
                taken[count++] = Pending[i];
                Pending[i].InUse = false;
            }
        }

        for (var i = 0; i < count; i++)
            Emit(taken[i]);
    }

    /// <summary>
    /// The verdict, in the terms of the question being asked.
    ///
    /// A_ACTIVE is decisive on its own: the server restaged an index at or
    /// before the elapsing one. B_LATE is decisive too: the packet left after
    /// that edge's own cycleStart, so the client had already begun it. B_RISK
    /// is suggestive only - see <see cref="FlightAllowanceMs"/>.
    /// </summary>
    private static string Verdict(in Record r)
    {
        if (!r.GeometryChanged)
            return r.HasOld ? "SAME" : "NOPREV";
        if (r.Offset <= 0)
            return "A_ACTIVE";
        if (r.SentAtMs <= 0)
            return "NOSEND";
        if (r.SendMarginMs <= 0)
            return "B_LATE";
        return r.SendMarginMs < FlightAllowanceMs ? "B_RISK" : "CLEAN";
    }

    private static void Emit(in Record r)
    {
        Interlocked.Increment(ref _emitted);

        var offset = r.Offset >= 0 ? $"e+{r.Offset}" : $"e{r.Offset}";
        var old = r.HasOld ? $"{r.OldFromX},{r.OldFromY}->{r.OldToX},{r.OldToY}" : "none";
        var sent = r.SentAtMs <= 0 ? "never" : r.SentAtMs.ToString();
        var sendMargin = r.SentAtMs <= 0 ? "nosend" : $"{r.SendMarginMs}ms";

        Log.Info(
            $"[MV2/replan {Verdict(r)}] unit={r.VirtualId} sess={r.WalkSessionId} src={r.From} " +
            $"rev={r.NewRevision} serverNow={r.ServerNowMs} timelineOrigin={r.TimelineOrigin} " +
            $"activeEdge={r.ActiveEdge} " +
            $"current={r.CurrentEdgeIndex}:{r.CurFromX},{r.CurFromY}->{r.CurToX},{r.CurToY} " +
            $"replacing={r.FirstIndex}({offset}) startTick={r.FirstIndexStartTick} " +
            $"old={old} new={r.NewFromX},{r.NewFromY}->{r.NewToX},{r.NewToY} " +
            $"planMargin={r.FirstIndexStartTick - r.ServerNowMs}ms sentAt={sent} sendMargin={sendMargin}");
    }
}

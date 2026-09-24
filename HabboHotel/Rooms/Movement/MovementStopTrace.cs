using System.Threading;
using NLog;

namespace Plus.HabboHotel.Rooms.Movement;

/// <summary>
/// DIAGNOSTIC ONLY: [MV2/STOP], one emulator console line per UNPLANNED stop of
/// a real player's walk. It changes no movement behaviour.
///
/// For the walk-end hitch: the client drew a step all the way to B while the
/// server ended the walk on A, and the next walk started from A - a whole
/// tile. The code allows two shapes of that, and this names which one it was:
///
///   midStep=true    the walk was stopped part-way through a step. A step
///                   commits only when it ends, so the walk ends on the step's
///                   ORIGIN (stopsOn) while the client is drawing it towards
///                   stepTo. msIntoStep is how far through it the client was.
///   midStep=false   stopped at a boundary, with the step just committed. edge
///                   is then the index the client may already have begun from
///                   preview, and msIntoStep is how far into it.
///
/// reason says who asked: halt:File.Member for MovementV2Bridge.Halt (stun,
/// knockout, medical bed), blocked, displacement, no-map. The ordinary route
/// end is NOT logged - every walk ends that way, and nothing is advertised past
/// its final step. Bots and pets are skipped.
///
/// serverNow is the movement clock, the same domain as the client's
/// estServerNow and JOIN-START's requestTime, so a line can be matched to a
/// browser capture by unit and time.
///
/// Cheap by construction: a stop is an event, never per frame, and NLog's
/// console target is an AsyncWrapper, so the write never blocks the movement
/// path. Capped at MaxLines per process; stopMidStep and stopUnplanned on
/// :movementstats keep counting past the cap.
/// </summary>
internal static class MovementStopTrace
{
    private static readonly Logger Log = LogManager.GetLogger("MovementV2");

    public const int MaxLines = 1000;

    public const string ReasonRouteEnd = "route-end";

    private static int _lines;

    /// <summary>Call BEFORE the walker is changed. Caller holds MovementLock.</summary>
    public static void Record(MovementState w, string reason)
    {
        if (w.Mode != MovementMode.Moving || !w.IsRealUser || reason == ReasonRouteEnd)
            return;

        var now = MovementScheduler.Instance.Clock.NowMs;
        var midStep = w.Tile != w.EdgeTo;

        MovementCounters.StopUnplanned(midStep);

        if (Interlocked.Increment(ref _lines) > MaxLines)
            return;

        Log.Info($"[MV2/STOP] unit={w.VirtualId} reason={reason} midStep={midStep} " +
                 $"msIntoStep={now - w.EdgeStartTick(w.EdgeIndex)} interval={w.IntervalMs} " +
                 $"stopsOn={w.Tile.X},{w.Tile.Y} stepTo={w.EdgeTo.X},{w.EdgeTo.Y} " +
                 $"session={w.WalkSessionId} rev={w.RouteRevision} edge={w.EdgeIndex} " +
                 $"elapsing={w.ElapsingEdgeIndex(now)} emittedThrough={w.EmittedThroughEdge} " +
                 $"routeLeft={w.Route.Length - w.Route.Cursor} serverNow={now}");
    }
}

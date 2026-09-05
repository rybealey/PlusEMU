using Plus.HabboHotel.GameClients;

namespace Plus.HabboHotel.Rooms.Movement;

/// <summary>
/// pixelrp Movement V2: paired timing trace for TWO avatars. Diagnostic only -
/// it reads state and whispers it, and changes no movement behaviour at all.
///
/// It exists to answer one question: when two avatars look out of step, is it
/// the SERVER timelines that disagree, or the client rendering them?
///
/// THE NUMBER THAT DECIDES IT is cycleStart % 500. Every edge start is derived
/// as TimelineOrigin + k * 500, so that modulus is the walker's phase against
/// the 500ms grid and is CONSTANT for a whole walk session. Two avatars whose
/// values differ are on timelines that will never line up, no matter how
/// perfect the client interpolation is - and no amount of client work can fix
/// it, because the server never described an aligned pair in the first place.
///
/// Off by default and gated per virtual id, so the hot path pays one volatile
/// read when nobody is being traced.
/// </summary>
public static class MovementTrace
{
    /// <summary>Per-avatar throttle. A 500ms edge samples at most twice.</summary>
    private const int ThrottleMs = 250;

    private static volatile GameClient? _sink;
    private static volatile int _idA = -1;
    private static volatile int _idB = -1;

    private static long _lastEmitA;
    private static long _lastEmitB;

    // Last observed grid phase per slot, so each line can carry the DELTA
    // rather than making the reader diff two whispers by eye.
    private static long _phaseA = -1;
    private static long _phaseB = -1;

    public static bool Enabled => _sink != null;

    public static int IdA => _idA;

    public static int IdB => _idB;

    public static void Start(GameClient sink, int virtualIdA, int virtualIdB)
    {
        _idA = virtualIdA;
        _idB = virtualIdB;
        Interlocked.Exchange(ref _lastEmitA, 0);
        Interlocked.Exchange(ref _lastEmitB, 0);
        Interlocked.Exchange(ref _phaseA, -1);
        Interlocked.Exchange(ref _phaseB, -1);
        _sink = sink;
    }

    public static void Stop()
    {
        _sink = null;
        _idA = -1;
        _idB = -1;
    }

    /// <summary>
    /// Called on the Q1 outbound thread immediately after an edge's 4110 goes
    /// out, so what is reported is exactly what the client was told.
    ///
    /// TimelineOrigin is DERIVED rather than read from MovementState:
    /// cycleStart = TimelineOrigin + EdgeIndex * IntervalMs by construction, so
    /// the subtraction is exact and this needs no access to the scheduler's
    /// state and no second lock.
    /// </summary>
    public static void OnEdgeEmitted(in MovementEdgeRecord edge, long serverNowMs)
    {
        var sink = _sink;
        if (sink == null)
            return;

        var isA = edge.VirtualId == _idA;
        var isB = edge.VirtualId == _idB;
        if (!isA && !isB)
            return;

        var interval = edge.IntervalMs > 0 ? edge.IntervalMs : MovementSettings.IntervalMs;
        var origin = edge.CycleStartMs - (long)edge.EdgeIndex * interval;
        var phase = ((edge.CycleStartMs % interval) + interval) % interval;

        if (isA)
            Interlocked.Exchange(ref _phaseA, phase);
        else
            Interlocked.Exchange(ref _phaseB, phase);

        // Throttle per slot, not globally, so one busy walker cannot crowd the
        // other one out of the comparison.
        var previous = isA ? Interlocked.Read(ref _lastEmitA) : Interlocked.Read(ref _lastEmitB);
        if (serverNowMs - previous < ThrottleMs)
            return;
        if (isA)
            Interlocked.Exchange(ref _lastEmitA, serverNowMs);
        else
            Interlocked.Exchange(ref _lastEmitB, serverNowMs);

        var otherPhase = isA ? Interlocked.Read(ref _phaseB) : Interlocked.Read(ref _phaseA);
        var delta = otherPhase < 0 ? "n/a" : $"{phase - otherPhase}ms";

        try
        {
            sink.SendWhisper(
                $"[MV2/srv {(isA ? "A" : "B")}] unit={edge.VirtualId} " +
                $"sess={edge.WalkSessionId} rev={edge.RouteRevision} edge={edge.EdgeIndex} " +
                $"origin={origin} cycleStart={edge.CycleStartMs} serverNow={serverNowMs} " +
                $"gridPhase={phase} vsOther={delta} " +
                $"tile={edge.FromX},{edge.FromY} -> {edge.ToX},{edge.ToY}.");
        }
        catch
        {
            // A dead sink must never disturb the movement path.
            _sink = null;
        }
    }
}

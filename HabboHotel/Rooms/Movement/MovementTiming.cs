using System.Diagnostics;

namespace Plus.HabboHotel.Rooms.Movement;

/// <summary>
/// Sub-500ms timing for :movementstats. Before this, the only lateness the
/// server counted was a beat more than a whole step (500ms) late - already
/// very visible - and nothing timed the route search, the waits for a room's
/// locks, or the time from a finished frame to the wire. Every movement change
/// was therefore chosen from code reading rather than data.
///
/// MEASUREMENT ONLY. Nothing here changes what the movement engine does, and
/// nothing that draws or decides reads it. The cost is a few interlocked
/// increments where each value is taken: no strings, no allocation and no
/// console I/O on the movement path (the page-stalling logging of a reverted
/// client change is the reason for that rule).
///
/// :movementstats prints each histogram; :movementstats reset zeroes them, so
/// a test can be read as "since I started" instead of "since the emulator
/// booted".
/// </summary>
public static class MovementTiming
{
    // Bucket upper bounds in microseconds. Two scales: step lateness runs in
    // milliseconds, locks and searches usually in tens of microseconds.
    private static readonly long[] LatenessEdgesUs = { 2_000, 5_000, 10_000, 25_000, 50_000, 100_000, 250_000, 500_000 };
    private static readonly long[] FastEdgesUs = { 50, 250, 1_000, 5_000, 25_000, 100_000 };
    private static readonly long[] TileEdges = { 25, 100, 400, 1_600, 6_400 };

    /// <summary>
    /// How late each walker's beat actually ran, with the clock read AFTER the
    /// scheduler has the room's movement lock. The scheduler's own clock is
    /// read before it waits for that lock, so the existing lateness never saw
    /// the wait.
    /// </summary>
    public static readonly MovementTimingHistogram StepLateness = new("stepLate", LatenessEdgesUs);

    /// <summary>The scheduler's wait for a room's movement lock, per room pass.</summary>
    public static readonly MovementTimingHistogram SchedulerLockWait = new("schedLockWait", FastEdgesUs);

    /// <summary>A click's wait for the room's movement lock (MovementV2Bridge.RequestMove).</summary>
    public static readonly MovementTimingHistogram ClickLockWait = new("clickLockWait", FastEdgesUs);

    /// <summary>
    /// How long a click then HOLDS that lock - route search and planning
    /// included. The scheduler cannot beat this room for as long as it lasts.
    /// </summary>
    public static readonly MovementTimingHistogram ClickLockHold = new("clickLockHold", FastEdgesUs);

    /// <summary>Time inside one route search, from every caller (click, redirect, blocked re-plan).</summary>
    public static readonly MovementTimingHistogram SearchTime = new("searchTime", FastEdgesUs);

    /// <summary>Tiles one route search looked at (expansions), not a time.</summary>
    public static readonly MovementTimingHistogram SearchTiles = new("searchTiles", TileEdges, isTime: false);

    /// <summary>
    /// From the beat that sealed a frame to the outbound thread picking it up.
    /// Measured from the serverNow the frame carries, which the scheduler reads
    /// at the start of that beat - so it also includes the beat's own lock wait.
    /// This is how stale "server time" is by the time the packet is built.
    /// </summary>
    public static readonly MovementTimingHistogram FrameToSend = new("frameToSend", LatenessEdgesUs);

    /// <summary>The outbound thread's wait for a room's _cycleLock before applying a frame.</summary>
    public static readonly MovementTimingHistogram SenderLockWait = new("senderLockWait", FastEdgesUs);

    /// <summary>How long applying and sending one frame holds _cycleLock.</summary>
    public static readonly MovementTimingHistogram SenderLockHold = new("senderLockHold", FastEdgesUs);

    /// <summary>
    /// How long the 500ms room tick (RoomUserManager.OnCycle) holds _cycleLock.
    /// A frame for this room that arrives meanwhile waits for it - and the
    /// single outbound thread waits with it, so every room's sends queue up.
    /// </summary>
    public static readonly MovementTimingHistogram RoomTickLockHold = new("roomTickLockHold", FastEdgesUs);

    private static readonly MovementTimingHistogram[] All =
    {
        StepLateness, SchedulerLockWait, ClickLockWait, ClickLockHold, SearchTime, SearchTiles,
        FrameToSend, SenderLockWait, SenderLockHold, RoomTickLockHold
    };

    private static long _sinceMs = SystemMovementClock.Instance.NowMs;

    /// <summary>A high-resolution timestamp for the measurements above.</summary>
    public static long Now() => Stopwatch.GetTimestamp();

    /// <summary>Microseconds since <paramref name="startedAt"/>, a value from <see cref="Now"/>.</summary>
    public static long MicrosSince(long startedAt) =>
        (long)((Stopwatch.GetTimestamp() - startedAt) * (1_000_000.0 / Stopwatch.Frequency));

    public static void Reset()
    {
        foreach (var histogram in All)
            histogram.Reset();
        Interlocked.Exchange(ref _sinceMs, SystemMovementClock.Instance.NowMs);
    }

    /// <summary>One whisper per histogram, headed by how long they have been counting.</summary>
    public static IEnumerable<string> Describe()
    {
        var seconds = (SystemMovementClock.Instance.NowMs - Interlocked.Read(ref _sinceMs)) / 1000;
        yield return $"[MV2/TIMING] counting for {seconds}s (:movementstats reset to restart)";
        foreach (var histogram in All)
            yield return histogram.Describe();
    }
}

/// <summary>
/// A fixed-bucket histogram updated with interlocked operations only, so any
/// thread can record into it without a lock. Values are microseconds (or plain
/// counts when <c>isTime</c> is false); each edge is a bucket's upper bound,
/// and one extra bucket holds everything above the last edge.
/// </summary>
public sealed class MovementTimingHistogram
{
    private readonly string _name;
    private readonly long[] _edges;
    private readonly long[] _counts;
    private readonly bool _isTime;
    private long _total;
    private long _sum;
    private long _max;

    public MovementTimingHistogram(string name, long[] edges, bool isTime = true)
    {
        _name = name;
        _edges = edges;
        _counts = new long[edges.Length + 1];
        _isTime = isTime;
    }

    public void Record(long value)
    {
        if (value < 0)
            value = 0;

        var bucket = 0;
        while (bucket < _edges.Length && value > _edges[bucket])
            bucket++;

        Interlocked.Increment(ref _counts[bucket]);
        Interlocked.Increment(ref _total);
        Interlocked.Add(ref _sum, value);

        long observed;
        while (value > (observed = Interlocked.Read(ref _max)))
        {
            if (Interlocked.CompareExchange(ref _max, value, observed) == observed)
                break;
        }
    }

    public void Reset()
    {
        for (var i = 0; i < _counts.Length; i++)
            Interlocked.Exchange(ref _counts[i], 0);
        Interlocked.Exchange(ref _total, 0);
        Interlocked.Exchange(ref _sum, 0);
        Interlocked.Exchange(ref _max, 0);
    }

    /// <summary>"stepLate n=812 avg=0.9ms max=31ms | &lt;=2ms:790 &lt;=5ms:14 ... &gt;500ms:0"</summary>
    public string Describe()
    {
        var total = Interlocked.Read(ref _total);
        var parts = new System.Text.StringBuilder();
        parts.Append(_name).Append(" n=").Append(total);

        if (total > 0)
        {
            parts.Append(" avg=").Append(Format(Interlocked.Read(ref _sum) / total));
            parts.Append(" max=").Append(Format(Interlocked.Read(ref _max)));
        }

        parts.Append(" |");
        for (var i = 0; i < _counts.Length; i++)
        {
            var label = (i < _edges.Length) ? ("<=" + Format(_edges[i])) : (">" + Format(_edges[_edges.Length - 1]));
            parts.Append(' ').Append(label).Append(':').Append(Interlocked.Read(ref _counts[i]));
        }

        return parts.ToString();
    }

    private string Format(long value)
    {
        if (!_isTime)
            return value.ToString();
        if (value < 1_000)
            return value + "us";
        if (value < 10_000)
            return (value / 1000.0).ToString("0.#", System.Globalization.CultureInfo.InvariantCulture) + "ms";
        return (value / 1000) + "ms";
    }
}

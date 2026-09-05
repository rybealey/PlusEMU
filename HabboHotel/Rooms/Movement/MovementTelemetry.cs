using System.Diagnostics;

namespace Plus.HabboHotel.Rooms.Movement;

/// <summary>
/// pixelrp Movement V2 (A1): the click-to-wire latency stages, plus the small
/// set of health counters that replace V1's console logging.
///
/// EVERY latency figure in the V2 architecture documents is an ESTIMATE. This
/// type exists so those estimates can be replaced with measurements before any
/// of them is quoted as fact.
///
/// Timestamps are Stopwatch ticks, NOT Environment.TickCount64: TickCount64's
/// ~15.6ms Windows granularity is coarser than every stage being measured here,
/// so it would report the thread hops as zero.
/// </summary>
public sealed class MovementLatencySample
{
    public long T0RequestParsed;
    public long T1LockAcquired;
    public long T2PathfindComplete;
    public long T3StagedAndSignalled;
    public long T4SchedulerWoke;
    public long T5FrameSealed;
    public long T6WorkerWoke;
    public long T7BytesQueued;

    public int VirtualId;
    public uint RoomId;

    public static long Stamp() => Stopwatch.GetTimestamp();

    public static double ToMs(long fromTicks, long toTicks)
    {
        if (fromTicks <= 0 || toTicks <= 0 || toTicks < fromTicks)
            return double.NaN;
        return (toTicks - fromTicks) * 1000.0 / Stopwatch.Frequency;
    }

    /// <summary>Total click-to-bytes, or NaN when the sample is incomplete.</summary>
    public double TotalMs => ToMs(T0RequestParsed, T7BytesQueued);

    public string Describe() =>
        $"[MOVEMENT_V2_LATENCY] room={RoomId} unit={VirtualId} " +
        $"parse->lock={ToMs(T0RequestParsed, T1LockAcquired):F3} " +
        $"lock->path={ToMs(T1LockAcquired, T2PathfindComplete):F3} " +
        $"path->staged={ToMs(T2PathfindComplete, T3StagedAndSignalled):F3} " +
        $"staged->schedWake={ToMs(T3StagedAndSignalled, T4SchedulerWoke):F3} " +
        $"schedWake->sealed={ToMs(T4SchedulerWoke, T5FrameSealed):F3} " +
        $"sealed->workerWake={ToMs(T5FrameSealed, T6WorkerWoke):F3} " +
        $"workerWake->bytes={ToMs(T6WorkerWoke, T7BytesQueued):F3} " +
        $"total={TotalMs:F3}";
}

/// <summary>
/// Health counters. Cheap interlocked increments only - no string formatting and
/// no console I/O on the movement path, which is what made V1's telemetry a
/// measurable cost on the tick (audit D3-D6).
/// </summary>
public static class MovementCounters
{
    private static long _orphansRecovered;
    private static long _drainDeferred;
    private static long _beatsLate;
    private static long _maxBeatLatenessMs;
    private static long _barrierWaits;
    private static long _roomFaults;
    private static long _pathfindCalls;
    private static long _pathfindPartial;
    private static long _pathfindFailed;

    // Stage counters. Added after two beta freezes that could not be located
    // from the outside: every aggregate looked healthy (no faults, no lateness,
    // no orphans, frames flowing) while avatars still stopped. These pin down
    // WHICH stage stops rather than proving the system is "fine" in aggregate.
    private static long _walkStarts;
    private static long _redirects;
    private static long _advances;
    private static long _commits;
    private static long _stopsRouteEnd;
    private static long _stopsBlocked;
    private static long _replans;
    private static long _roomProcessed;
    private static long _drainedWalkers;

    // The scheduler thread's own faults, as distinct from a room's. Before the
    // per-iteration isolation these did not exist as a category: an exception
    // outside the per-room try simply ended the thread, so the counter that
    // would have named the freeze was never incremented and roomFaults - the
    // only fault counter there was - stayed convincingly at zero.
    private static long _schedulerFaults;
    private static string _lastSchedulerFault = "none";
    private static long _lastSchedulerFaultAtMs;

    public static long SchedulerFaults => Interlocked.Read(ref _schedulerFaults);
    public static string LastSchedulerFault => Volatile.Read(ref _lastSchedulerFault);
    public static long LastSchedulerFaultAtMs => Interlocked.Read(ref _lastSchedulerFaultAtMs);

    /// <summary>
    /// Record a fault that escaped one scheduler beat. Keeps the type and the
    /// first frame only: this is read over a whisper, not a log file.
    /// </summary>
    public static void SchedulerFault(Exception e)
    {
        Interlocked.Increment(ref _schedulerFaults);
        Interlocked.Exchange(ref _lastSchedulerFaultAtMs, SystemMovementClock.Instance.NowMs);
        try
        {
            var trace = e.StackTrace ?? string.Empty;
            var cut = trace.IndexOf('\n');
            var frame = (cut >= 0 ? trace.Substring(0, cut) : trace).Trim();
            if (frame.Length > 160)
                frame = frame.Substring(0, 160);
            Volatile.Write(ref _lastSchedulerFault, $"{e.GetType().Name}: {e.Message} @ {frame}");
        }
        catch
        {
            Volatile.Write(ref _lastSchedulerFault, e.GetType().Name);
        }
        Plus.Core.ExceptionLogger.LogCriticalException(e);
    }

    public static void WalkStart() => Interlocked.Increment(ref _walkStarts);
    public static void Redirect() => Interlocked.Increment(ref _redirects);
    public static void Advance() => Interlocked.Increment(ref _advances);
    public static void Commit() => Interlocked.Increment(ref _commits);
    public static void StopRouteEnd() => Interlocked.Increment(ref _stopsRouteEnd);
    public static void StopBlocked() => Interlocked.Increment(ref _stopsBlocked);
    public static void Replan() => Interlocked.Increment(ref _replans);
    public static void RoomProcessed() => Interlocked.Increment(ref _roomProcessed);
    public static void DrainedWalker() => Interlocked.Increment(ref _drainedWalkers);

    public static string StageSnapshot() =>
        $"starts={Interlocked.Read(ref _walkStarts)} " +
        $"redirects={Interlocked.Read(ref _redirects)} " +
        $"roomProcessed={Interlocked.Read(ref _roomProcessed)} " +
        $"drained={Interlocked.Read(ref _drainedWalkers)} " +
        $"advances={Interlocked.Read(ref _advances)} " +
        $"commits={Interlocked.Read(ref _commits)} " +
        $"replans={Interlocked.Read(ref _replans)} " +
        $"stopEnd={Interlocked.Read(ref _stopsRouteEnd)} " +
        $"stopBlocked={Interlocked.Read(ref _stopsBlocked)}";

    public static void OrphanRecovered() => Interlocked.Increment(ref _orphansRecovered);
    public static void DrainDeferred() => Interlocked.Increment(ref _drainDeferred);
    public static void BarrierWait() => Interlocked.Increment(ref _barrierWaits);
    public static void RoomFault() => Interlocked.Increment(ref _roomFaults);
    public static void PathfindCall() => Interlocked.Increment(ref _pathfindCalls);
    public static void PathfindPartial() => Interlocked.Increment(ref _pathfindPartial);
    public static void PathfindFailed() => Interlocked.Increment(ref _pathfindFailed);

    public static void BeatLate(long lateMs)
    {
        Interlocked.Increment(ref _beatsLate);
        long observed;
        while (lateMs > (observed = Interlocked.Read(ref _maxBeatLatenessMs)))
        {
            if (Interlocked.CompareExchange(ref _maxBeatLatenessMs, lateMs, observed) == observed)
                break;
        }
    }

    public static string Snapshot() =>
        $"[MOVEMENT_V2_COUNTERS] orphansRecovered={Interlocked.Read(ref _orphansRecovered)} " +
        $"drainDeferred={Interlocked.Read(ref _drainDeferred)} " +
        $"beatsLate={Interlocked.Read(ref _beatsLate)} " +
        $"maxBeatLatenessMs={Interlocked.Read(ref _maxBeatLatenessMs)} " +
        $"barrierWaits={Interlocked.Read(ref _barrierWaits)} " +
        $"roomFaults={Interlocked.Read(ref _roomFaults)} " +
        $"schedulerFaults={Interlocked.Read(ref _schedulerFaults)} " +
        $"pathfind={Interlocked.Read(ref _pathfindCalls)} " +
        $"partial={Interlocked.Read(ref _pathfindPartial)} " +
        $"failed={Interlocked.Read(ref _pathfindFailed)}";
}

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
        $"pathfind={Interlocked.Read(ref _pathfindCalls)} " +
        $"partial={Interlocked.Read(ref _pathfindPartial)} " +
        $"failed={Interlocked.Read(ref _pathfindFailed)}";
}

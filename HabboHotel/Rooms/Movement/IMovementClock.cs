using System.Diagnostics;

namespace Plus.HabboHotel.Rooms.Movement;

/// <summary>
/// pixelrp Movement V2 (A1): the ONLY source of time for the movement system.
///
/// V1 read <c>Environment.TickCount64</c> and <c>DateTime.Now</c> from a dozen
/// places, which is why none of its timing behaviour could be tested without
/// real wall-clock waits. V2 routes every timing decision through this
/// interface so the scheduler, the promise model and the stall paths can all be
/// driven by a fake clock in unit tests.
///
/// NowMs is MONOTONIC milliseconds. It is not wall-clock time and must never be
/// compared against DateTime, a database timestamp, or a value from another
/// process. Only differences between two NowMs readings are meaningful.
/// </summary>
public interface IMovementClock
{
    long NowMs { get; }
}

/// <summary>
/// Production clock. Backed by <see cref="Stopwatch"/> rather than
/// <c>Environment.TickCount64</c> because TickCount64 has ~15.6ms granularity on
/// Windows, which is coarse enough to make the intra-server latency stages in
/// <see cref="MovementTelemetry"/> read as zero.
///
/// The epoch is process start; callers must only ever use differences.
/// </summary>
public sealed class SystemMovementClock : IMovementClock
{
    public static readonly SystemMovementClock Instance = new();

    private readonly Stopwatch _stopwatch = Stopwatch.StartNew();

    public long NowMs => _stopwatch.ElapsedMilliseconds;
}


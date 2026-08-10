using System;

namespace Plus.Utilities;

/// <summary>
///     Drift-free fixed-rate scheduler. Deadlines sit at absolute multiples of the
///     period on a caller-supplied monotonic clock, so time spent processing a beat
///     (or waiting on a lock) never pushes later beats back. A wakeup that arrives
///     past one or more deadlines skips them instead of firing back-to-back.
/// </summary>
public sealed class FixedRateSchedule
{
    private readonly long _periodMs;
    private readonly Func<long> _now;
    private long _nextDeadlineMs;

    public FixedRateSchedule(long periodMs, Func<long> monotonicNowMs)
    {
        _periodMs = periodMs;
        _now = monotonicNowMs;
        _nextDeadlineMs = monotonicNowMs() + periodMs;
    }

    /// <summary>
    ///     Call after processing a beat (or before the first): returns milliseconds to
    ///     sleep until the next unfired deadline. Always in (0, period].
    /// </summary>
    public int DelayUntilNextBeat()
    {
        var now = _now();
        var deadline = _nextDeadlineMs;
        while (deadline <= now)
            deadline += _periodMs;
        _nextDeadlineMs = deadline + _periodMs;
        return (int)(deadline - now);
    }

    /// <summary>Milliseconds left until <paramref name="lastMs" /> + <paramref name="periodMs" />, clamped at 0.</summary>
    public static long RemainingUntil(long lastMs, long periodMs, long nowMs) => Math.Max(0, lastMs + periodMs - nowMs);
}

using Plus.Utilities;
using Xunit;

namespace Plus.Tests;

public class FixedRateScheduleTests
{
    [Fact]
    public void FirstDelayIsOneFullPeriod()
    {
        long now = 10_000;
        var schedule = new FixedRateSchedule(500, () => now);

        Assert.Equal(500, schedule.DelayUntilNextBeat());
    }

    [Fact]
    public void ProcessingTimeDoesNotAccumulateIntoTheSchedule()
    {
        long now = 0;
        var schedule = new FixedRateSchedule(500, () => now);

        Assert.Equal(500, schedule.DelayUntilNextBeat()); // deadline 500
        now = 620; // woke at 500, beat processing took 120ms
        Assert.Equal(380, schedule.DelayUntilNextBeat()); // deadline 1000 held
        now = 1010; // 10ms of processing this time
        Assert.Equal(490, schedule.DelayUntilNextBeat()); // deadline 1500 held
    }

    [Fact]
    public void LateWakeupSkipsMissedBeatsWithoutBursting()
    {
        long now = 0;
        var schedule = new FixedRateSchedule(500, () => now);

        Assert.Equal(500, schedule.DelayUntilNextBeat()); // deadline 500
        now = 1730; // overran through deadlines 1000 and 1500
        Assert.Equal(270, schedule.DelayUntilNextBeat()); // skips to deadline 2000
        now = 2000;
        Assert.Equal(500, schedule.DelayUntilNextBeat()); // deadline 2500, cadence restored
    }

    [Fact]
    public void WakeupExactlyOnDeadlineWaitsAFullPeriod()
    {
        long now = 0;
        var schedule = new FixedRateSchedule(500, () => now);

        Assert.Equal(500, schedule.DelayUntilNextBeat()); // deadline 500
        now = 1000; // landed exactly on deadline 1000 after processing beat 500
        Assert.Equal(500, schedule.DelayUntilNextBeat()); // deadline 1000 is spent; next is 1500
    }

    [Fact]
    public void DelayIsNeverZeroOrNegative()
    {
        long now = 0;
        var schedule = new FixedRateSchedule(500, () => now);

        for (var i = 0; i < 50; i++)
        {
            var delay = schedule.DelayUntilNextBeat();
            Assert.InRange(delay, 1, 500);
            now += delay + i * 37 % 900; // wake on time, then drift erratically
        }
    }

    [Theory]
    [InlineData(0, 500, 200, 300)] // mid-cooldown: 300ms left until the beat
    [InlineData(0, 500, 500, 0)]   // beat reached: due now
    [InlineData(0, 500, 900, 0)]   // long past: due now, never negative
    public void RemainingUntilClampsAtZero(long last, long period, long now, long expected)
    {
        Assert.Equal(expected, FixedRateSchedule.RemainingUntil(last, period, now));
    }
}

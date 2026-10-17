using Plus.Communication.Packets.Outgoing.Users;

namespace Plus.HabboHotel.Weather;

/// <summary>
/// pixelrp: the city's own clock (Twist, 2026-10-18).
///
/// A whole in-game day - 24 hours - passes in 90 real minutes: day, 7 AM to
/// 7 PM, takes 45 of them, and night, 7 PM to 7 AM, the other 45. One in-game
/// hour is 3 minutes 45 seconds; one in-game minute is 3.75 real seconds.
///
/// NOTHING IS STORED. The time is worked out from the real clock (Unix time
/// modulo the 90-minute cycle), so every server, every restart and every
/// player agrees on it with nothing to save or lose. Staff can still hold it
/// (the City Panel's Time switch, WeatherStation.PinnedMinutes): a pin stops
/// this clock at that minute, for the HUD clock and the sky alike.
///
/// The weather is NOT this clock's: it stays the real San Francisco reading
/// (WeatherStation). Only the day/night look follows the game time.
/// </summary>
public static class GameClock
{
    /// <summary>Real milliseconds in one in-game day.</summary>
    public const int CycleMs = 90 * 60 * 1000;

    public const int MinutesPerDay = 24 * 60;

    /// <summary>Morning: 7 AM.</summary>
    public const int DayStartsAt = 7 * 60;

    /// <summary>Night: 7 PM.</summary>
    public const int NightStartsAt = 19 * 60;

    /// <summary>How far into the current 90-minute cycle the real clock is, in real ms.</summary>
    public static int CycleOffsetMs() => (int)(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() % CycleMs);

    /// <summary>The in-game time of day in minutes after midnight (0-1439), or the staff pin.</summary>
    public static int Minutes()
    {
        var pinned = WeatherStation.PinnedMinutes;
        if (pinned >= 0)
            return pinned;
        return (int)((long)CycleOffsetMs() * MinutesPerDay / CycleMs);
    }

    /// <summary>7 AM up to (not including) 7 PM.</summary>
    public static bool IsDay() => Minutes() is >= DayStartsAt and < NightStartsAt;

    /// <summary>What a client needs to run the clock itself (sent at login and whenever a pin changes).</summary>
    public static RpGameClockComposer Compose() => new(CycleOffsetMs(), CycleMs, WeatherStation.PinnedMinutes);
}

using Dapper;

namespace Plus.HabboHotel.Corporations;

/// <summary>
/// pixelrp: the corporation week (Corporation Panel). It turns over on
/// Sunday at 11:59 PM UK time - warnings, strikes and shifts roll over then,
/// management chat does not. A week is named for the Monday it mostly is, and
/// shifts are bucketed by UK calendar day (rp_corporation_shift_days).
///
/// The reset itself is one row in rp_corporation_weeks and a zeroed
/// shift_seconds_week. A minute timer checks for it, so a reset the emulator
/// was down for runs at the next boot instead of being skipped. Everything
/// week-scoped that is a record (warnings, strikes) is filtered by time rather
/// than wiped, so the panel can still show last week.
/// </summary>
public static class CorpWeek
{
    private static NLog.ILogger Log => NLog.LogManager.GetLogger("Plus.HabboHotel.Corporations.CorpWeek");

    private static readonly TimeZoneInfo Uk = FindUk();
    private static System.Threading.Timer _timer;
    private static readonly object ResetLock = new();

    // The moment the week turns over, in UK wall-clock time.
    private static readonly TimeSpan ResetTimeOfDay = new(23, 59, 0);

    private static TimeZoneInfo FindUk()
    {
        foreach (var id in new[] { "Europe/London", "GMT Standard Time" })
        {
            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById(id);
            }
            catch (TimeZoneNotFoundException)
            {
            }
        }
        NLog.LogManager.GetLogger("Plus.HabboHotel.Corporations.CorpWeek").Warn("No Europe/London time zone on this host; the corporation week turns over in UTC.");
        return TimeZoneInfo.Utc;
    }

    public static void Init()
    {
        lock (ResetLock)
        {
            // First boot with the panel: the current week is simply recorded,
            // so this week's counters are not wiped halfway through.
            using var connection = PlusEnvironment.DatabaseManager.Connection();
            var any = connection.ExecuteScalar<int>("SELECT COUNT(*) FROM `rp_corporation_weeks`");
            if (any == 0)
                connection.Execute("INSERT IGNORE INTO `rp_corporation_weeks` (`week_start`, `reset_at`) VALUES (@week, UNIX_TIMESTAMP())",
                    new { week = WeekOf(DateTimeOffset.UtcNow).ToString("yyyy-MM-dd") });
        }
        CheckReset();
        _timer = new System.Threading.Timer(_ => CheckReset(), null, 60000, 60000);
    }

    /// <summary>UK wall-clock time for a UTC moment.</summary>
    public static DateTime ToUk(DateTimeOffset utc) => TimeZoneInfo.ConvertTime(utc, Uk).DateTime;

    /// <summary>The UK calendar day a moment falls on - the shift-day bucket.</summary>
    public static DateTime DayOf(DateTimeOffset utc) => ToUk(utc).Date;

    /// <summary>
    /// The Monday naming the week a moment falls in. The last minute of a
    /// Sunday (from 11:59 PM) already belongs to the next week.
    /// </summary>
    public static DateTime WeekOf(DateTimeOffset utc)
    {
        var uk = ToUk(utc);
        var date = uk.Date;
        if (uk.DayOfWeek == DayOfWeek.Sunday && uk.TimeOfDay >= ResetTimeOfDay)
            date = date.AddDays(1);
        var sinceMonday = ((int)date.DayOfWeek + 6) % 7;
        return date.AddDays(-sinceMonday);
    }

    /// <summary>The Monday of the current week, minus <paramref name="weeksAgo"/> weeks.</summary>
    public static DateTime WeekStart(int weeksAgo = 0) => WeekOf(DateTimeOffset.UtcNow).AddDays(-7 * weeksAgo);

    /// <summary>When the week named by <paramref name="monday"/> ends: the Sunday after at 11:59 PM UK, as unix seconds.</summary>
    public static int ResetAt(DateTime monday)
    {
        var local = DateTime.SpecifyKind(monday.AddDays(6).Add(ResetTimeOfDay), DateTimeKind.Unspecified);
        return (int)new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(local, Uk)).ToUnixTimeSeconds();
    }

    /// <summary>When the week named by <paramref name="monday"/> began: the Sunday before at 11:59 PM UK.</summary>
    public static int StartedAt(DateTime monday) => ResetAt(monday.AddDays(-7));

    private static void CheckReset()
    {
        try
        {
            lock (ResetLock)
            {
                var week = WeekOf(DateTimeOffset.UtcNow).ToString("yyyy-MM-dd");
                using var connection = PlusEnvironment.DatabaseManager.Connection();
                // The row is the claim: a second emulator (or a second tick)
                // inserting the same week affects nothing and resets nothing.
                var claimed = connection.Execute(
                    "INSERT IGNORE INTO `rp_corporation_weeks` (`week_start`, `reset_at`) VALUES (@week, UNIX_TIMESTAMP())", new { week });
                if (claimed == 0)
                    return;
                connection.Execute("UPDATE `rp_corporation_employees` SET `shift_seconds_week` = 0");
                connection.Execute("DELETE FROM `rp_corporation_shift_days` WHERE `day` < @cutoff",
                    new { cutoff = WeekStart(12).ToString("yyyy-MM-dd") });
                Log.Info($"Corporation week {week} started: weekly shifts reset.");
            }
        }
        catch (Exception e)
        {
            Log.Error(e, "Corporation week reset failed; it will be tried again in a minute.");
            return;
        }
        // Profiles and infostands show this week's shifts: send everyone the
        // zeroed counters. A reset run at boot has nobody to tell yet.
        try
        {
            if (PlusEnvironment.Game?.ClientManager != null)
                CorporationUtility.BroadcastAllEmployments();
        }
        catch (Exception e)
        {
            Log.Warn(e, "Corporation week reset done, but the new counters could not be sent.");
        }
    }
}

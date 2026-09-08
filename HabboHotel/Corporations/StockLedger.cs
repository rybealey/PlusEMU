using Dapper;

namespace Plus.HabboHotel.Corporations;

/// <summary>
/// pixelrp: the stock ledger behind the phone's Stocks app.
///
/// rp_corporations.stock is a single live number with no history, and a chart
/// needs a series. So this samples every corporation's stock on a fixed
/// interval and keeps 90 days of readings; the app reads the series back and
/// nothing has to be computed on demand.
///
/// The interval is deliberately coarse. Stock only moves when a player sells
/// something, so sampling faster than that records silence - and this is a
/// low-needs informational app that should cost the server almost nothing.
/// The app itself asks for current values when it opens and refreshes while
/// it is open; nothing here runs per-player.
/// </summary>
public static class StockLedger
{
    /// <summary>How often a reading is taken. 144 samples a day per corporation.</summary>
    public const int SampleIntervalMinutes = 10;

    /// <summary>How much history is kept. Older readings are purged on the tick.</summary>
    public const int RetentionDays = 90;

    /// <summary>
    /// The most readings any one corporation returns for a window. Sized so a
    /// ONE-DAY window is lossless at the current interval (144 readings) -
    /// that is the range players actually sit on, and striding it would throw
    /// away detail for nothing. Longer windows hold far more (a 3-month range
    /// is ~13,000 readings, pointless on a 300px chart and rude on the wire)
    /// and are strided down to this many evenly spaced points.
    /// </summary>
    public const int MaxPointsPerCorp = 144;

    private static System.Threading.Timer _timer;

    public static void Init()
    {
        // First reading a minute after boot so a restart leaves a mark, then
        // on the interval. Purging rides the same tick - it is one indexed
        // DELETE and there is no reason to own a second timer for it.
        var period = (int)TimeSpan.FromMinutes(SampleIntervalMinutes).TotalMilliseconds;

        _timer = new System.Threading.Timer(_ => Tick(), null, 60000, period);
    }

    /// <summary>
    /// Stamp one reading immediately, outside the tick. :setstock uses this so
    /// a value a staff member just changed shows on the chart at once instead
    /// of waiting up to ten minutes for the next sample.
    /// </summary>
    public static void Record(int corporationId, int value)
    {
        try
        {
            using var connection = PlusEnvironment.DatabaseManager.Connection();

            connection.Execute(
                "INSERT INTO `rp_corporation_stock_samples` (`corporation_id`, `value`, `sampled_at`) " +
                "VALUES (@corporationId, @value, @now)",
                new { corporationId, value, now = (int)DateTimeOffset.UtcNow.ToUnixTimeSeconds() });
        }
        catch (Exception e)
        {
            NLog.LogManager.GetLogger("Plus.HabboHotel.Corporations.StockLedger")
                .Error("Stock reading failed: {0}", e.Message);
        }
    }

    private static void Tick()
    {
        try
        {
            var now = (int)DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var cutoff = now - (int)TimeSpan.FromDays(RetentionDays).TotalSeconds;

            using var connection = PlusEnvironment.DatabaseManager.Connection();

            // One row per corporation, straight from the live column - no
            // round trip through the emulator, so a corporation added while
            // the server is up is sampled without anything being told.
            connection.Execute(
                "INSERT INTO `rp_corporation_stock_samples` (`corporation_id`, `value`, `sampled_at`) " +
                "SELECT `id`, `stock`, @now FROM `rp_corporations`", new { now });

            connection.Execute(
                "DELETE FROM `rp_corporation_stock_samples` WHERE `sampled_at` < @cutoff", new { cutoff });
        }
        catch (Exception e)
        {
            NLog.LogManager.GetLogger("Plus.HabboHotel.Corporations.StockLedger")
                .Error("Stock sample failed: {0}", e.Message);
        }
    }
}

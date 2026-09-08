using Dapper;
using Plus.Communication.Packets.Outgoing.Users;
using Plus.HabboHotel.Corporations;
using Plus.HabboHotel.GameClients;

namespace Plus.Communication.Packets.Incoming.Users;

/// <summary>
/// pixelrp: the Stocks app asking for the board - current stock and capacity
/// for every corporation, plus the sample series for the window requested.
///
/// Open to every player: which corporations are running low is exactly what a
/// farmer or miner opens the app to find out, and none of it is private.
/// </summary>
internal class RpGetStocksEvent : IPacketEvent
{
    // The ranges the app offers, in minutes. Anything else is clamped to a day
    // so a malformed request cannot ask for the whole table.
    private static readonly int[] Windows = { 1440, 10080, 43200, 129600 };

    public Task Parse(GameClient session, IIncomingPacket packet)
    {
        if (session.GetHabbo() == null)
            return Task.CompletedTask;

        var requested = packet.ReadInt();
        var windowMinutes = Windows.Contains(requested) ? requested : Windows[0];
        var since = (int)DateTimeOffset.UtcNow.ToUnixTimeSeconds() - (windowMinutes * 60);

        using var connection = PlusEnvironment.DatabaseManager.Connection();

        var corps = connection.Query<(int Id, string Acronym, string Name, string Description, int Stock, int Capacity)>(
            "SELECT `id`, `acronym`, `name`, `description`, `stock`, `stock_capacity` AS Capacity " +
            "FROM `rp_corporations` WHERE `acronym` != '' ORDER BY `sort_order`, `id`").ToList();

        var rows = connection.Query<(int CorporationId, int SampledAt, int Value)>(
            "SELECT `corporation_id` AS CorporationId, `sampled_at` AS SampledAt, `value` " +
            "FROM `rp_corporation_stock_samples` WHERE `sampled_at` >= @since " +
            "ORDER BY `corporation_id`, `sampled_at`", new { since }).ToList();

        var payload = corps.Select(corp =>
        {
            var series = rows.Where(row => row.CorporationId == corp.Id)
                .Select(row => new RpStocksComposer.Sample(row.SampledAt, row.Value))
                .ToList();

            return new RpStocksComposer.CorpStock(
                corp.Id, corp.Acronym, corp.Name, corp.Description,
                corp.Stock, corp.Capacity, Stride(series));
        }).ToList();

        session.Send(new RpStocksComposer(payload, windowMinutes));
        return Task.CompletedTask;
    }

    /// <summary>
    /// Thin a series down to at most MaxPointsPerCorp evenly spaced readings,
    /// always keeping the newest one - a three-month window is thousands of
    /// samples and the chart is 300px wide. Under the cap it passes through.
    /// </summary>
    private static List<RpStocksComposer.Sample> Stride(List<RpStocksComposer.Sample> series)
    {
        var max = StockLedger.MaxPointsPerCorp;

        if (series.Count <= max)
            return series;

        var step = (double)series.Count / max;
        var thinned = new List<RpStocksComposer.Sample>(max);

        for (var i = 0; i < max; i++)
            thinned.Add(series[(int)(i * step)]);

        // The last reading is the one the player is looking at, so it must be
        // the real latest rather than whatever the stride happened to land on.
        thinned[^1] = series[^1];

        return thinned;
    }
}

using System.Collections.Concurrent;
using Dapper;
using Plus.HabboHotel.Corporations;

namespace Plus.HabboHotel.CityPanel;

/// <summary>
/// pixelrp: what the city's services cost (rp_service_prices, migration 235) -
/// medkits and heals at the hospital, energy, snacks and the passive drink at
/// The Muse. Set in the City Panel's Economy tab; each service reads its price
/// here when its billing ships. A key with no row costs 0.
/// </summary>
public static class ServicePrices
{
    public sealed class Price
    {
        public string Key { get; set; } = "";
        public int CorporationId { get; set; }
        public string Name { get; set; } = "";
        public int Amount { get; set; }
    }

    private static ConcurrentDictionary<string, Price>? _cache;

    private static ConcurrentDictionary<string, Price> Cache
    {
        get
        {
            if (_cache != null)
                return _cache;
            using var connection = PlusEnvironment.DatabaseManager.Connection();
            var rows = connection.Query<Price>(
                "SELECT `key` AS `Key`, `corporation_id` AS CorporationId, `name` AS Name, `price` AS Amount " +
                "FROM `rp_service_prices` ORDER BY `sort_order`, `key`");
            _cache = new ConcurrentDictionary<string, Price>(rows.ToDictionary(row => row.Key));
            return _cache;
        }
    }

    /// <summary>What a service costs, in coins.</summary>
    public static int Get(string key) => Cache.TryGetValue(key, out var price) ? price.Amount : 0;

    /// <summary>Every price, in the order the panel lists them.</summary>
    public static List<Price> All()
    {
        using var connection = PlusEnvironment.DatabaseManager.Connection();
        var order = connection.Query<string>("SELECT `key` FROM `rp_service_prices` ORDER BY `sort_order`, `key`").ToList();
        return order.Where(key => Cache.ContainsKey(key)).Select(key => Cache[key]).ToList();
    }

    /// <summary>Change one price. False for a key that does not exist.</summary>
    public static bool Set(string key, int amount, int staffId)
    {
        if (!Cache.TryGetValue(key, out var price))
            return false;
        amount = Math.Clamp(amount, 0, CityEconomy.MaxPrice);
        using (var connection = PlusEnvironment.DatabaseManager.Connection())
            connection.Execute("UPDATE `rp_service_prices` SET `price` = @amount, `updated_by` = @staffId, `updated_at` = UNIX_TIMESTAMP() WHERE `key` = @key LIMIT 1",
                new { amount, staffId, key });
        price.Amount = amount;
        return true;
    }
}

/// <summary>
/// The City Panel's Economy tab: pay per rank for every corporation, who is on
/// shift, and the service prices.
/// </summary>
public static class CityEconomy
{
    /// <summary>The most a rank may be paid per pay interval (ten minutes on shift).</summary>
    public const int MaxPay = 10_000;

    /// <summary>The most a service may cost.</summary>
    public const int MaxPrice = 100_000;

    public sealed class Rank
    {
        public int Id { get; set; }
        public int CorporationId { get; set; }
        public string Name { get; set; } = "";
        public int Pay { get; set; }
    }

    public sealed class Corp
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
        /// <summary>Left out of the Corporations window (Settings > Hide corporation).</summary>
        public bool Hidden { get; set; }
        public List<Rank> Ranks { get; } = new();
        public List<(int UserId, string Username, string RankName)> OnShift { get; } = new();
    }

    public static List<Corp> Corporations()
    {
        List<Corp> corps;
        List<Rank> ranks;
        using (var connection = PlusEnvironment.DatabaseManager.Connection())
        {
            corps = connection.Query<Corp>("SELECT `id` AS Id, `name` AS Name, `hidden` AS Hidden FROM `rp_corporations` ORDER BY `sort_order`, `id`").ToList();
            ranks = connection.Query<Rank>(
                "SELECT `id` AS Id, `corporation_id` AS CorporationId, `name` AS Name, `pay` AS Pay " +
                "FROM `rp_corporation_ranks` ORDER BY `rank_order` DESC").ToList();
        }
        var byId = corps.ToDictionary(corp => corp.Id);
        foreach (var rank in ranks)
            if (byId.TryGetValue(rank.CorporationId, out var corp))
                corp.Ranks.Add(rank);

        var rankNames = ranks.ToDictionary(rank => rank.Id, rank => rank.Name);
        foreach (var (userId, corpId, rankId) in ShiftManager.OnDuty())
        {
            if (!byId.TryGetValue(corpId, out var corp))
                continue;
            var habbo = PlusEnvironment.Game.ClientManager.GetClientByUserId(userId)?.GetHabbo();
            corp.OnShift.Add((userId, habbo?.Username ?? $"#{userId}", rankNames.GetValueOrDefault(rankId, "")));
        }
        return corps;
    }

    // ---- live shifts ----------------------------------------------------------

    /// <summary>A burst of shift changes (a shift change of crew) goes out as one update.</summary>
    private const int ShiftsPushDelayMs = 500;

    private static int _shiftsPushPending;

    /// <summary>
    /// Somebody clocked in or out (ShiftManager): send the Economy tab's state
    /// to every staff member who can open the City Panel, so On shift now is
    /// live. Coalesced - a burst of changes within half a second is one send -
    /// and off the caller's thread, since the caller is the shift tick or a
    /// packet. A client without the Economy tab open ignores it.
    /// </summary>
    public static void ShiftsChanged()
    {
        if (Interlocked.Exchange(ref _shiftsPushPending, 1) == 1)
            return;
        _ = Task.Run(async () =>
        {
            await Task.Delay(ShiftsPushDelayMs);
            Interlocked.Exchange(ref _shiftsPushPending, 0);
            try
            {
                var staff = PlusEnvironment.Game.ClientManager.GetClients
                    .Where(client => CityPanelAccess.CanOpen(client?.GetHabbo()))
                    .ToList();
                if (staff.Count == 0)
                    return;
                var packet = new Communication.Packets.Outgoing.CityPanel.RpCityEconomyComposer(Corporations(), ServicePrices.All());
                foreach (var client in staff)
                    client.Send(packet);
            }
            catch (Exception)
            {
                // A live refresh is a nicety; the tab still loads on open.
            }
        });
    }

    /// <summary>Corporation settings, by number - RpCityCorpSettingEvent's.</summary>
    public const int SettingHidden = 1;

    /// <summary>Change one of a corporation's settings. False for a corporation or setting that does not exist.</summary>
    public static bool SetCorpSetting(int corpId, int setting, int value)
    {
        if (setting != SettingHidden)
            return false;
        using var connection = PlusEnvironment.DatabaseManager.Connection();
        return connection.Execute("UPDATE `rp_corporations` SET `hidden` = @hidden WHERE `id` = @corpId LIMIT 1",
            new { hidden = value == 1 ? 1 : 0, corpId }) > 0;
    }

    /// <summary>
    /// Set a rank's pay. Everyone on duty at it is refreshed at once
    /// (BroadcastAllEmployments re-reads it into their live shift), so their
    /// next payday is the new figure. False for a rank that does not exist.
    /// </summary>
    public static bool SetPay(int rankId, int pay)
    {
        pay = Math.Clamp(pay, 0, MaxPay);
        int corpId;
        using (var connection = PlusEnvironment.DatabaseManager.Connection())
        {
            corpId = connection.QuerySingleOrDefault<int>("SELECT `corporation_id` FROM `rp_corporation_ranks` WHERE `id` = @rankId LIMIT 1", new { rankId });
            if (corpId == 0)
                return false;
            connection.Execute("UPDATE `rp_corporation_ranks` SET `pay` = @pay WHERE `id` = @rankId LIMIT 1", new { pay, rankId });
        }
        CorporationUtility.BroadcastAllEmployments(corpId);
        return true;
    }
}

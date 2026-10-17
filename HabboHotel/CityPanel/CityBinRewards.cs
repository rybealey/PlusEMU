using Dapper;
using Plus.HabboHotel.Corporations;
using Plus.HabboHotel.Rooms.Chat.Commands.Moderator;

namespace Plus.HabboHotel.CityPanel;

/// <summary>
/// pixelrp: what searching a bin can turn up (rp_bin_rewards, migration 273) -
/// set in the City Panel's Economy tab, Bin Search. Each row is one possible
/// find with a weight: a search picks one row, each row's chance being its
/// weight over the total, and pays an amount between its min and max. A row
/// is coins, one of the Consumables, or nothing at all.
///
/// Read fresh on every search, not cached: a search is ten seconds of
/// somebody's time and one small query, and a change made in the panel - or
/// by hand in the database - counts from the very next search.
/// </summary>
public static class BinRewards
{
    public const string Nothing = "nothing";
    public const string Coins = "coins";

    private static readonly NLog.ILogger Log = NLog.LogManager.GetLogger("Plus.HabboHotel.CityPanel.BinRewards");

    public const int MaxWeight = 1000;
    public const int MaxCoins = 10_000;
    public const int MaxRows = 30;

    public sealed class Reward
    {
        public int Id { get; set; }
        public string Key { get; set; } = "";
        public int Weight { get; set; }
        public int Min { get; set; }
        public int Max { get; set; }
    }

    /// <summary>
    /// The consumables a bin may give besides coins - the basics only (Twist,
    /// 2026-10-18). A consumable added to the game later goes on this list,
    /// and from then on staff can put it in bins from the panel. Nothing off
    /// it ever comes out of a bin - VIP tokens, weapons and police gear
    /// included - however the table is edited.
    /// </summary>
    public static readonly IReadOnlyList<string> Consumables = new[] { "medkit", "snack", "smoothie" };

    /// <summary>
    /// What a row can be, with its name: nothing, coins, then the consumables
    /// above, named as staff spawn them.
    /// </summary>
    public static List<(string Key, string Name)> Options()
    {
        var options = new List<(string Key, string Name)> { (Nothing, "Nothing"), (Coins, "Coins") };
        options.AddRange(SpawnCommand.Spawnable.Where(item =>
            Consumables.Contains(item.ItemKey) && !PoliceUtility.IsPoliceGear(item.ItemKey)));
        return options;
    }

    public static string NameOf(string key) =>
        Options().FirstOrDefault(option => option.Key == key).Name ?? key;

    /// <summary>Every row, in the order the panel lists them.</summary>
    public static List<Reward> All()
    {
        using var connection = PlusEnvironment.DatabaseManager.Connection();
        return connection.Query<Reward>(
            "SELECT `id` AS Id, `reward` AS `Key`, `weight` AS Weight, `min_amount` AS `Min`, `max_amount` AS `Max` " +
            "FROM `rp_bin_rewards` ORDER BY `sort_order`, `id`").ToList();
    }

    /// <summary>
    /// One row picked by weight, or null when nothing has any weight - which
    /// the search treats the same as finding nothing.
    /// </summary>
    public static Reward? Roll()
    {
        var rows = All().Where(row => row.Weight > 0).ToList();
        var total = rows.Sum(row => (long)Math.Min(row.Weight, MaxWeight));
        if (total <= 0)
            return null;
        var pick = Random.Shared.NextInt64(total);
        foreach (var row in rows)
        {
            var weight = Math.Min(row.Weight, MaxWeight);
            if (pick < weight)
                return row;
            pick -= weight;
        }
        return null;
    }

    /// <summary>
    /// What a picked row actually pays, held to the same rules Save keeps:
    /// only a reward on the list, never the force's equipment, and an amount
    /// inside its bounds. The table can be written by hand as well as from
    /// the panel, so the payout checks for itself rather than trusting the
    /// row. Anything outside the rules pays nothing.
    /// </summary>
    public static (string Key, int Amount) Payout(Reward? reward)
    {
        if (reward == null || reward.Key == Nothing)
            return (Nothing, 0);
        if (PoliceUtility.IsPoliceGear(reward.Key) || !Options().Any(option => option.Key == reward.Key))
        {
            Log.Warn($"rp_bin_rewards row {reward.Id} has a reward that is not allowed ('{reward.Key}'); it paid nothing.");
            return (Nothing, 0);
        }
        var cap = reward.Key == Coins ? MaxCoins : Users.Habbo.RpStackCap;
        var floor = reward.Key == Coins ? 0 : 1;
        var min = Math.Clamp(reward.Min, floor, cap);
        var max = Math.Clamp(reward.Max, min, cap);
        var amount = Random.Shared.Next(min, max + 1);
        return amount > 0 ? (reward.Key, amount) : (Nothing, 0);
    }

    /// <summary>
    /// Add a row (id 0) or change one, kept in bounds: coins up to MaxCoins,
    /// items one stack at most, nothing without an amount. Null when saved,
    /// otherwise what to tell the staff member.
    /// </summary>
    public static string? Save(int id, string key, int weight, int min, int max, int staffId)
    {
        key = (key ?? "").Trim();
        if (!Options().Any(option => option.Key == key))
            return "That reward isn't on the list.";
        weight = Math.Clamp(weight, 0, MaxWeight);
        var cap = key == Coins ? MaxCoins : (key == Nothing ? 0 : Users.Habbo.RpStackCap);
        var floor = (key == Coins || key == Nothing) ? 0 : 1;
        min = Math.Clamp(min, floor, cap);
        max = Math.Clamp(max, min, cap);

        using var connection = PlusEnvironment.DatabaseManager.Connection();
        if (id <= 0)
        {
            if (connection.ExecuteScalar<int>("SELECT COUNT(*) FROM `rp_bin_rewards`") >= MaxRows)
                return $"A bin can hold at most {MaxRows} rewards.";
            connection.Execute(
                "INSERT INTO `rp_bin_rewards` (`reward`,`weight`,`min_amount`,`max_amount`,`sort_order`,`updated_by`,`updated_at`) " +
                "SELECT @key, @weight, @min, @max, COALESCE(MAX(`sort_order`), 0) + 1, @staffId, UNIX_TIMESTAMP() FROM `rp_bin_rewards`",
                new { key, weight, min, max, staffId });
            return null;
        }
        // Asked separately: MySQL counts an UPDATE that changes nothing as no
        // rows, and the same values saved twice in a second are exactly that.
        if (connection.ExecuteScalar<int>("SELECT COUNT(*) FROM `rp_bin_rewards` WHERE `id` = @id", new { id }) == 0)
            return "That reward no longer exists.";
        connection.Execute(
            "UPDATE `rp_bin_rewards` SET `reward` = @key, `weight` = @weight, `min_amount` = @min, `max_amount` = @max, " +
            "`updated_by` = @staffId, `updated_at` = UNIX_TIMESTAMP() WHERE `id` = @id LIMIT 1",
            new { id, key, weight, min, max, staffId });
        return null;
    }

    /// <summary>Take a row out. False when it was already gone.</summary>
    public static bool Delete(int id)
    {
        using var connection = PlusEnvironment.DatabaseManager.Connection();
        return connection.Execute("DELETE FROM `rp_bin_rewards` WHERE `id` = @id LIMIT 1", new { id }) > 0;
    }
}

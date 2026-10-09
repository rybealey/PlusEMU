using Dapper;

namespace Plus.HabboHotel.CityPanel;

/// <summary>
/// pixelrp City Panel: the Economy tab's Global Ledger - every movement of
/// money in the city, newest first, from the two ledgers that between them see
/// all of it: coins on hand (rp_coin_ledger, migration 239) and the bank's
/// checking and savings accounts (rp_bank_transactions, migration 110).
///
/// Read only. A search names players (a username prefix, resolved to ids
/// first so both ledgers are read through their user_id keys) and the filter
/// picks a side; pages are fixed-size with an offset, which is plenty for a
/// staff window that is read from the top.
/// </summary>
public static class CityLedger
{
    public const int PageSize = 50;

    public const int FilterAll = 0;
    public const int FilterHand = 1;
    public const int FilterBank = 2;

    public sealed class Row
    {
        public long Id { get; set; }
        public int CreatedAt { get; set; }
        public int UserId { get; set; }
        public string Username { get; set; } = "";
        /// <summary>hand | current | savings</summary>
        public string Account { get; set; } = "";
        /// <summary>The bank's movement kind (BankTransactionKind); empty for coins on hand.</summary>
        public string Kind { get; set; } = "";
        public long Amount { get; set; }
        public long BalanceAfter { get; set; }
        public string Source { get; set; } = "";
    }

    /// <summary>What the city holds right now.</summary>
    public sealed class Totals
    {
        public long Hand { get; set; }
        public long Checking { get; set; }
        public long Savings { get; set; }
        public int Accounts { get; set; }
    }

    private sealed class UserCredits
    {
        public int Id { get; set; }
        public int Credits { get; set; }
    }

    /// <summary>One page, and whether there is another after it.</summary>
    public static (List<Row> Rows, bool More) Page(string query, int filter, int offset)
    {
        offset = Math.Max(0, offset);
        query = (query ?? "").Trim();
        using var connection = PlusEnvironment.DatabaseManager.Connection();

        List<int>? ids = null;
        if (query.Length > 0)
        {
            var like = query.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_") + "%";
            ids = connection.Query<int>("SELECT `id` FROM `users` WHERE `username` LIKE @like ORDER BY `username` LIMIT 50", new { like }).ToList();
            if (ids.Count == 0)
                return (new List<Row>(), false);
        }

        var who = ids == null ? "" : " WHERE `user_id` IN @ids";
        var parts = new List<string>();
        if (filter != FilterBank)
            parts.Add("SELECT `id` AS Id, `created_at` AS CreatedAt, `user_id` AS UserId, `username` AS Username, 'hand' AS Account, " +
                      "'' AS Kind, `amount` AS Amount, `balance_after` AS BalanceAfter, `source` AS Source FROM `rp_coin_ledger`" + who);
        if (filter != FilterHand)
            parts.Add("SELECT `id` AS Id, `created_at` AS CreatedAt, `user_id` AS UserId, `username` AS Username, `account` AS Account, " +
                      "`kind` AS Kind, `amount` AS Amount, `balance_after` AS BalanceAfter, `source` AS Source FROM `rp_bank_transactions`" + who);

        var rows = connection.Query<Row>(
            "SELECT * FROM (" + string.Join(" UNION ALL ", parts) + ") AS ledger " +
            "ORDER BY CreatedAt DESC, Id DESC LIMIT @limit OFFSET @offset",
            new { ids, limit = PageSize + 1, offset }).ToList();
        var more = rows.Count > PageSize;
        if (more)
            rows.RemoveAt(rows.Count - 1);
        return (rows, more);
    }

    public static Totals CityTotals()
    {
        using var connection = PlusEnvironment.DatabaseManager.Connection();
        var totals = connection.QuerySingle<Totals>(
            "SELECT COALESCE(SUM(`current_balance`), 0) AS Checking, COALESCE(SUM(`savings_balance`), 0) AS Savings, " +
            "COUNT(*) AS Accounts FROM `rp_bank_accounts`");

        // Coins on hand: users.credits is only written at logout (and by the
        // odd crash hedge), so for whoever is online the live figure replaces
        // the stored one.
        var hand = connection.ExecuteScalar<long>("SELECT COALESCE(SUM(`credits`), 0) FROM `users`");
        var online = PlusEnvironment.Game.ClientManager.GetClients
            .Select(client => client?.GetHabbo())
            .Where(habbo => habbo != null)
            .ToDictionary(habbo => habbo!.Id, habbo => habbo!.Credits);
        if (online.Count > 0)
        {
            var ids = online.Keys.ToList();
            foreach (var stored in connection.Query<UserCredits>("SELECT `id` AS Id, `credits` AS Credits FROM `users` WHERE `id` IN @ids", new { ids }))
                hand += online[stored.Id] - stored.Credits;
        }
        totals.Hand = hand;
        return totals;
    }
}

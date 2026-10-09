namespace Plus.HabboHotel.Users.Banking;

/// <summary>
/// pixelrp: every change to a player's coins ON HAND, with why - the half of
/// the money the bank's own ledger (rp_bank_transactions) does not see. Read
/// by the City Panel's Global Ledger beside it (rp_coin_ledger, migration 239).
///
/// Written where the coins move, next to the in-memory change, one row per
/// movement with the balance it left. The crash-hedge `UPDATE users SET
/// credits` writes that follow some of them are persistence of the same
/// movement, not a second one, and are not logged.
/// </summary>
public static class CoinLedger
{
    private const int MaxSourceLength = 96;

    public static void Record(Habbo? habbo, int delta, string source)
    {
        if (habbo == null)
            return;
        Record(habbo.Id, habbo.Username, delta, habbo.Credits, source);
    }

    public static void Record(int userId, string username, int delta, int balanceAfter, string source)
    {
        if (delta == 0 || userId <= 0)
            return;
        source = (source ?? "").Trim();
        if (source.Length > MaxSourceLength)
            source = source[..MaxSourceLength];
        try
        {
            using var dbClient = PlusEnvironment.DatabaseManager.GetQueryReactor();
            dbClient.SetQuery("INSERT INTO `rp_coin_ledger` (`user_id`, `username`, `amount`, `balance_after`, `source`, `created_at`) " +
                              "VALUES (@user, @username, @amount, @balance, @source, UNIX_TIMESTAMP())");
            dbClient.AddParameter("user", userId);
            dbClient.AddParameter("username", username ?? "");
            dbClient.AddParameter("amount", delta);
            dbClient.AddParameter("balance", balanceAfter);
            dbClient.AddParameter("source", source);
            dbClient.RunQuery();
        }
        catch (Exception)
        {
            // The ledger is a record of the money, never a gate on it: a failed
            // write must not undo or block the movement it describes.
        }
    }
}

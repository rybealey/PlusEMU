using Plus.Communication.Packets.Outgoing.Users;

namespace Plus.HabboHotel.Corporations;

/// <summary>
/// pixelrp: who is wanted, and how badly.
///
/// A player's wanted level is the HIGHEST severity among their open charges,
/// not the sum - five speeding tickets are not a murder (98_CrimeSeverity).
/// Changing that rule is changing the MAX below to something else; nothing
/// else in the hotel encodes it.
///
/// The whole list goes to everyone rather than each player being told only
/// their own: the Wanted window is a public noticeboard, and the HUD draws
/// stars over whoever you are looking at, so every client needs every entry
/// anyway. It is small by construction - only players with an open charge are
/// on it.
/// </summary>
public static class WantedUtility
{
    /// <summary>One line of a rap sheet: the crime, and how many counts of it are open.</summary>
    public record WantedCharge(string Name, int Count);

    /// <summary>
    /// How long a player stays wanted after their LATEST charge. Every new
    /// charge restarts the clock; the charges themselves stay on the sheet.
    /// </summary>
    public const int WantedSeconds = 15 * 60;

    /// <remarks><c>Remaining</c> is seconds until they drop off the list, sent
    /// relative rather than as a timestamp so a client's clock cannot skew it.</remarks>
    public record WantedPlayer(int UserId, string Username, string Figure, int Level, int Remaining, List<WantedCharge> Charges);

    public static List<WantedPlayer> GetWanted()
    {
        var wanted = new List<WantedPlayer>();
        using var dbClient = PlusEnvironment.DatabaseManager.GetQueryReactor();
        // Severity is the star count, so MAX(severity) IS the wanted level.
        // MAX(charged_at) is the latest charge, which is what the wanted
        // window counts down from: a player is listed for WantedSeconds after
        // it, and only players still inside that window are returned.
        dbClient.SetQuery(
            "SELECT u.`id` AS user_id, u.`username`, u.`look`, " +
            "MAX(c.`severity`) AS level, MAX(ch.`charged_at`) AS latest " +
            "FROM `rp_charges` ch " +
            "JOIN `rp_crimes` c ON c.`id` = ch.`crime_id` " +
            "JOIN `users` u ON u.`id` = ch.`user_id` " +
            "WHERE ch.`dropped_at` = 0 " +
            "GROUP BY u.`id`, u.`username`, u.`look` " +
            "HAVING latest > UNIX_TIMESTAMP() - @window " +
            "ORDER BY level DESC, latest DESC");
        dbClient.AddParameter("window", WantedSeconds);
        var table = dbClient.GetTable();
        if (table == null)
            return wanted;
        var charges = GetOpenCharges(dbClient);
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        foreach (System.Data.DataRow row in table.Rows)
        {
            var userId = Convert.ToInt32(row["user_id"]);
            var remaining = (int)Math.Max(1, Convert.ToInt64(row["latest"]) + WantedSeconds - now);
            wanted.Add(new WantedPlayer(
                userId,
                Convert.ToString(row["username"]) ?? "",
                Convert.ToString(row["look"]) ?? "",
                Convert.ToInt32(row["level"]),
                remaining,
                charges.TryGetValue(userId, out var sheet) ? sheet : new List<WantedCharge>()));
        }
        return wanted;
    }

    /// <summary>
    /// Every open charge in the hotel, grouped per player and collapsed per
    /// crime - two counts of assault are one line reading "Assault ×2". This
    /// is what the Wanted window shows on hover, so the order is the order a
    /// reader wants: worst crime first, then by name.
    /// </summary>
    private static Dictionary<int, List<WantedCharge>> GetOpenCharges(Plus.Database.Interfaces.IQueryAdapter dbClient)
    {
        var sheets = new Dictionary<int, List<WantedCharge>>();
        dbClient.SetQuery(
            "SELECT ch.`user_id`, c.`name`, COUNT(*) AS counts " +
            "FROM `rp_charges` ch " +
            "JOIN `rp_crimes` c ON c.`id` = ch.`crime_id` " +
            "WHERE ch.`dropped_at` = 0 " +
            "GROUP BY ch.`user_id`, c.`id`, c.`name`, c.`severity` " +
            "ORDER BY ch.`user_id`, c.`severity` DESC, c.`name` ASC");
        var table = dbClient.GetTable();
        if (table == null)
            return sheets;
        foreach (System.Data.DataRow row in table.Rows)
        {
            var userId = Convert.ToInt32(row["user_id"]);
            if (!sheets.TryGetValue(userId, out var sheet))
                sheets[userId] = sheet = new List<WantedCharge>();
            sheet.Add(new WantedCharge(Convert.ToString(row["name"]) ?? "", Convert.ToInt32(row["counts"])));
        }
        return sheets;
    }

    /// <summary>
    /// Push the list to every online client. Called after a charge is filed.
    /// Expiry needs no push: each client counts its own entries down and drops
    /// them at zero. A charge dropped straight in the database (housekeeping)
    /// is not seen here, so that lands on the next charge or the next login.
    /// </summary>
    public static void Broadcast()
    {
        PlusEnvironment.Game.ClientManager.SendPacket(new RpWantedComposer(GetWanted()));
    }
}

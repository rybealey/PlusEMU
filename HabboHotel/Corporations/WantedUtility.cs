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
    /// The statute of limitations: how long a charge can sit on a sheet before
    /// it lapses. Every new charge restarts the clock for the WHOLE sheet -
    /// a player under active investigation does not get to run out the timer
    /// on their older counts - and when it runs out the sheet clears itself.
    /// </summary>
    public const int WantedSeconds = 15 * 60;

    /// <remarks><c>Remaining</c> is seconds until they drop off the list, sent
    /// relative rather than as a timestamp so a client's clock cannot skew it.</remarks>
    public record WantedPlayer(int UserId, string Username, string Figure, int Level, int Remaining, List<WantedCharge> Charges);

    public static List<WantedPlayer> GetWanted()
    {
        var wanted = new List<WantedPlayer>();
        using var dbClient = PlusEnvironment.DatabaseManager.GetQueryReactor();
        ExpireLapsed(dbClient);
        // Severity is the star count, so MAX(severity) IS the wanted level.
        // MAX(charged_at) is the latest charge, which is what the countdown
        // runs from. The HAVING is redundant after the sweep above - a lapsed
        // sheet has no open charges left to find - and stays as a guard, so a
        // sweep that is ever skipped cannot put a lapsed player on the list.
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
    /// Drop every charge on a sheet whose newest charge has passed the statute
    /// of limitations. Whole sheets, never single counts: the clock belongs to
    /// the sheet, so either all of it has lapsed or none of it has.
    ///
    /// Swept lazily, from the reads themselves, rather than on a timer. There
    /// is nothing to react to when a charge lapses - no packet goes out, no
    /// bubble, the clients already counted the player off their own lists -
    /// so the only thing that has to be true is that nothing ever READS a
    /// lapsed charge, and running it here makes that so. The cost is that a
    /// hotel where nobody logs in or charges anyone leaves lapsed rows sitting
    /// open until it wakes up.
    /// </summary>
    private static void ExpireLapsed(Plus.Database.Interfaces.IQueryAdapter dbClient)
    {
        dbClient.SetQuery(
            "UPDATE `rp_charges` ch " +
            "JOIN (SELECT `user_id`, MAX(`charged_at`) AS latest FROM `rp_charges` " +
            "      WHERE `dropped_at` = 0 GROUP BY `user_id`) sheet ON sheet.`user_id` = ch.`user_id` " +
            "SET ch.`dropped_at` = UNIX_TIMESTAMP() " +
            "WHERE ch.`dropped_at` = 0 AND sheet.`latest` <= UNIX_TIMESTAMP() - @window");
        dbClient.AddParameter("window", WantedSeconds);
        dbClient.RunQuery();
    }

    /// <summary>
    /// Sweep lapsed sheets without reading the list back - for the commands
    /// that ask a question about someone's record before anything is pushed.
    /// </summary>
    public static void ExpireLapsed()
    {
        using var dbClient = PlusEnvironment.DatabaseManager.GetQueryReactor();
        ExpireLapsed(dbClient);
    }

    /// <summary>
    /// Push the list to every online client. Called after a charge is filed
    /// or a sheet is pardoned. A sheet LAPSING needs no push: every client
    /// counts its own entries down and drops them at zero, arriving at the
    /// same answer the sweep does. A charge dropped straight in the database
    /// (housekeeping) is not seen here, so that lands on the next charge or
    /// the next login.
    /// </summary>
    public static void Broadcast()
    {
        PlusEnvironment.Game.ClientManager.SendPacket(new RpWantedComposer(GetWanted()));
    }
}

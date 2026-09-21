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
    /// <summary>
    /// One line of a rap sheet: the crime, and how many counts of it are open.
    /// The id rides along so the Wanted list's x can name the crime it is
    /// dropping a count of - the name is display text and two crimes may share
    /// one after a housekeeping rename.
    /// </summary>
    public record WantedCharge(int CrimeId, string Name, int Count);

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

    /// <summary>
    /// Who is in the hotel right now. THE list is the authority here rather
    /// than `users`.`online`, which is a row written on the way past and can
    /// outlive an unclean shutdown.
    ///
    /// Empty when nobody is connected, which every caller has to mean "nobody"
    /// and not "no filter" - an empty IN list that fell through to no WHERE at
    /// all would sweep the whole hotel's charges on an idle server.
    /// </summary>
    private static HashSet<int> OnlineUserIds()
    {
        var ids = new HashSet<int>();
        foreach (var client in PlusEnvironment.Game.ClientManager.GetClients)
        {
            var habbo = client?.GetHabbo();
            if (habbo != null)
                ids.Add(habbo.Id);
        }
        return ids;
    }

    /// <summary>
    /// Resume a returning player's clock where they left it.
    ///
    /// The countdown only runs while somebody is IN the hotel: logging out
    /// freezes it, logging back in starts it again from the same number. Since
    /// the clock is stored as the moment of the charge, freezing it means
    /// pushing that moment forward by however long they were away.
    ///
    /// Capped at now, so a player who was charged and immediately vanished for
    /// a week comes back to the full window rather than to a charge dated in
    /// the future.
    ///
    /// `last_online` is written on logout, so it is exactly the moment the
    /// clock stopped. A crash that never wrote it leaves the gap overstated
    /// and hands back more time than was earned - the forgiving direction, and
    /// the only one available without a heartbeat.
    /// </summary>
    public static void ResumeAfterOffline(int userId)
    {
        using var dbClient = PlusEnvironment.DatabaseManager.GetQueryReactor();
        dbClient.SetQuery(
            "UPDATE `rp_charges` ch " +
            "JOIN `users` u ON u.`id` = ch.`user_id` " +
            "SET ch.`charged_at` = LEAST(ch.`charged_at` + (UNIX_TIMESTAMP() - u.`last_online`), UNIX_TIMESTAMP()) " +
            "WHERE ch.`user_id` = @userId AND ch.`dropped_at` = 0 " +
            "  AND u.`last_online` > 0 AND u.`last_online` < UNIX_TIMESTAMP()");
        dbClient.AddParameter("userId", userId);
        dbClient.RunQuery();
    }

    public static List<WantedPlayer> GetWanted()
    {
        var wanted = new List<WantedPlayer>();
        var online = OnlineUserIds();
        if (online.Count == 0)
            return wanted;
        using var dbClient = PlusEnvironment.DatabaseManager.GetQueryReactor();
        ExpireLapsed(dbClient, online);
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
            // Offline players are off the noticeboard. Their sheet is intact
            // and their clock is stopped (see ExpireLapsed and
            // ResumeAfterOffline) - they simply are not here to be wanted, and
            // an officer cannot act on a name that cannot be found in a room.
            if (!online.Contains(userId))
                continue;
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
            "SELECT ch.`user_id`, c.`id` AS crime_id, c.`name`, COUNT(*) AS counts " +
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
            sheet.Add(new WantedCharge(
                Convert.ToInt32(row["crime_id"]),
                Convert.ToString(row["name"]) ?? "",
                Convert.ToInt32(row["counts"])));
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
    private static void ExpireLapsed(Plus.Database.Interfaces.IQueryAdapter dbClient, HashSet<int> online)
    {
        // ONLY the sheets of players who are here. A clock that keeps running
        // while somebody is logged out would expire their charges in absentia,
        // which is the opposite of freezing it - they would return to a clean
        // sheet having simply waited the hotel out.
        if (online.Count == 0)
            return;
        dbClient.SetQuery(
            "UPDATE `rp_charges` ch " +
            "JOIN (SELECT `user_id`, MAX(`charged_at`) AS latest FROM `rp_charges` " +
            "      WHERE `dropped_at` = 0 GROUP BY `user_id`) sheet ON sheet.`user_id` = ch.`user_id` " +
            "SET ch.`dropped_at` = UNIX_TIMESTAMP() " +
            "WHERE ch.`dropped_at` = 0 AND sheet.`latest` <= UNIX_TIMESTAMP() - @window " +
            "  AND ch.`user_id` IN (" + string.Join(",", online) + ")");
        dbClient.AddParameter("window", WantedSeconds);
        dbClient.RunQuery();
    }

    /// <summary>
    /// Sweep lapsed sheets without reading the list back - for the commands
    /// that ask a question about someone's record before anything is pushed.
    /// </summary>
    public static void ExpireLapsed()
    {
        var online = OnlineUserIds();
        if (online.Count == 0)
            return;
        using var dbClient = PlusEnvironment.DatabaseManager.GetQueryReactor();
        ExpireLapsed(dbClient, online);
    }

    /// <summary>What a dropped count leaves behind, for the officer's whisper.</summary>
    public record DroppedCharge(string Username, string CrimeName, int Remaining);

    /// <summary>
    /// Drop ONE open count of a crime from a player's sheet, oldest first, and
    /// report what is left of that crime. Null when there was nothing to drop
    /// - a lapsed sheet, an already-dropped count, two officers clicking the
    /// same x - which is a normal race, not an error.
    ///
    /// One row per call by design: the tooltip's x means "this count", and a
    /// stacked crime is a tally an officer walks down one click at a time.
    /// Clearing a whole sheet at once is :pardon.
    /// </summary>
    public static DroppedCharge DropOneCharge(int userId, int crimeId)
    {
        using var dbClient = PlusEnvironment.DatabaseManager.GetQueryReactor();
        // ORDER BY id: the oldest open count goes first, so a sheet reads as a
        // queue rather than losing whichever row the engine happened to find.
        dbClient.SetQuery(
            "UPDATE `rp_charges` SET `dropped_at` = UNIX_TIMESTAMP() " +
            "WHERE `user_id` = @user AND `crime_id` = @crime AND `dropped_at` = 0 " +
            "ORDER BY `id` ASC LIMIT 1");
        dbClient.AddParameter("user", userId);
        dbClient.AddParameter("crime", crimeId);
        dbClient.RunQuery();

        // Exactly what the UPDATE touched, on the same connection, rather than
        // a guess from timestamps: nothing to drop and a second officer having
        // dropped it a moment ago look identical in the data, and both mean
        // "not you". ROW_COUNT() is 0 or 1 - the UPDATE is LIMIT 1.
        dbClient.SetQuery("SELECT ROW_COUNT()");
        if (dbClient.GetInteger() == 0)
            return null;

        dbClient.SetQuery(
            "SELECT u.`username`, c.`name`, " +
            "(SELECT COUNT(*) FROM `rp_charges` ch WHERE ch.`user_id` = u.`id` " +
            " AND ch.`crime_id` = c.`id` AND ch.`dropped_at` = 0) AS remaining " +
            "FROM `users` u JOIN `rp_crimes` c ON c.`id` = @crime WHERE u.`id` = @user LIMIT 1");
        dbClient.AddParameter("user", userId);
        dbClient.AddParameter("crime", crimeId);
        var row = dbClient.GetRow();
        if (row == null)
            return null;

        return new DroppedCharge(
            Convert.ToString(row["username"]) ?? "",
            Convert.ToString(row["name"]) ?? "",
            Convert.ToInt32(row["remaining"]));
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

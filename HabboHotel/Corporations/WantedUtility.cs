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
    public record WantedPlayer(int UserId, string Username, string Figure, int Level, int Since);

    public static List<WantedPlayer> GetWanted()
    {
        var wanted = new List<WantedPlayer>();
        using var dbClient = PlusEnvironment.DatabaseManager.GetQueryReactor();
        // Severity is the star count, so MAX(severity) IS the wanted level.
        // MIN(charged_at) is when they first became wanted, which is what the
        // list sorts and counts from - a later charge does not reset it.
        dbClient.SetQuery(
            "SELECT u.`id` AS user_id, u.`username`, u.`look`, " +
            "MAX(c.`severity`) AS level, MIN(ch.`charged_at`) AS since " +
            "FROM `rp_charges` ch " +
            "JOIN `rp_crimes` c ON c.`id` = ch.`crime_id` " +
            "JOIN `users` u ON u.`id` = ch.`user_id` " +
            "WHERE ch.`dropped_at` = 0 " +
            "GROUP BY u.`id`, u.`username`, u.`look` " +
            "ORDER BY level DESC, since ASC");
        var table = dbClient.GetTable();
        if (table == null)
            return wanted;
        foreach (System.Data.DataRow row in table.Rows)
        {
            wanted.Add(new WantedPlayer(
                Convert.ToInt32(row["user_id"]),
                Convert.ToString(row["username"]) ?? "",
                Convert.ToString(row["look"]) ?? "",
                Convert.ToInt32(row["level"]),
                Convert.ToInt32(row["since"])));
        }
        return wanted;
    }

    /// <summary>
    /// Push the list to every online client. Called after a charge is filed;
    /// a charge dropped straight in the database (housekeeping) is not seen
    /// here, so that lands on the next charge or the next login.
    /// </summary>
    public static void Broadcast()
    {
        PlusEnvironment.Game.ClientManager.SendPacket(new RpWantedComposer(GetWanted()));
    }
}

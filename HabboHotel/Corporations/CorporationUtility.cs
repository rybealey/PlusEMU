using Dapper;
using Plus.HabboHotel.GameClients;
using Plus.Communication.Packets.Outgoing.Users;

namespace Plus.HabboHotel.Corporations;

/// <summary>
/// pixelrp: employment lookups shared by room entry, profile opens and the
/// hire commands. Corporations change rarely; the queries are cheap joins.
/// </summary>
public static class CorporationUtility
{
    public record Employment(int UserId, int CorpId, string Badge, string CorpName, string RankName, int Tier, int ShiftSeconds, int ShiftSecondsWeek);

    public static Employment GetEmployment(int userId)
    {
        using var connection = PlusEnvironment.DatabaseManager.Connection();
        return connection.QuerySingleOrDefault<Employment>(
            "SELECT e.`user_id` AS UserId, c.`id` AS CorpId, c.`badge` AS Badge, c.`name` AS CorpName, r.`name` AS RankName, e.`tier` AS Tier, " +
            "e.`shift_seconds` AS ShiftSeconds, e.`shift_seconds_week` AS ShiftSecondsWeek " +
            "FROM `rp_corporation_employees` e " +
            "INNER JOIN `rp_corporations` c ON c.`id` = e.`corporation_id` " +
            "INNER JOIN `rp_corporation_ranks` r ON r.`id` = e.`rank_id` " +
            "WHERE e.`user_id` = @userId LIMIT 1", new { userId });
    }

    public static List<Employment> GetEmployments(IEnumerable<int> userIds)
    {
        var ids = userIds.Distinct().ToList();
        if (!ids.Any())
            return new List<Employment>();
        using var connection = PlusEnvironment.DatabaseManager.Connection();
        return connection.Query<Employment>(
            "SELECT e.`user_id` AS UserId, c.`id` AS CorpId, c.`badge` AS Badge, c.`name` AS CorpName, r.`name` AS RankName, e.`tier` AS Tier, " +
            "e.`shift_seconds` AS ShiftSeconds, e.`shift_seconds_week` AS ShiftSecondsWeek " +
            "FROM `rp_corporation_employees` e " +
            "INNER JOIN `rp_corporations` c ON c.`id` = e.`corporation_id` " +
            "INNER JOIN `rp_corporation_ranks` r ON r.`id` = e.`rank_id` " +
            "WHERE e.`user_id` IN @ids", new { ids }).ToList();
    }

    public record TargetUser(int Id, string Username, GameClient Client);

    /// <summary>
    /// pixelrp: resolve a username to a user id, online or offline. Client
    /// is null when the player is offline (announcements should skip them).
    /// </summary>
    public static TargetUser ResolveUser(string username)
    {
        var client = PlusEnvironment.Game.ClientManager.GetClientByUsername(username);
        if (client?.GetHabbo() != null)
            return new TargetUser(client.GetHabbo().Id, client.GetHabbo().Username, client);
        using var connection = PlusEnvironment.DatabaseManager.Connection();
        var row = connection.QuerySingleOrDefault<(int Id, string Username)?>(
            "SELECT `id`, `username` FROM `users` WHERE `username` = @username LIMIT 1", new { username });
        return row == null ? null : new TargetUser(row.Value.Id, row.Value.Username, null);
    }

    public record ManagerContext(int CorpId, string CorpName, int RankOrder, int ManageRankOrder);

    /// <summary>
    /// pixelrp: the gate every corp-management command (:hire, :fire, future
    /// promotions) passes through - the actor must be employed, at or above
    /// their corporation's manage_rank_order, and clocked in. Whispers the
    /// reason and returns null when the gate fails.
    /// </summary>
    public static ManagerContext RequireManager(GameClient session)
    {
        var userId = session.GetHabbo().Id;
        ManagerContext context;
        using (var connection = PlusEnvironment.DatabaseManager.Connection())
            context = connection.QuerySingleOrDefault<ManagerContext>(
                "SELECT c.`id` AS CorpId, c.`name` AS CorpName, r.`rank_order` AS RankOrder, c.`manage_rank_order` AS ManageRankOrder " +
                "FROM `rp_corporation_employees` e " +
                "INNER JOIN `rp_corporation_ranks` r ON r.`id` = e.`rank_id` " +
                "INNER JOIN `rp_corporations` c ON c.`id` = e.`corporation_id` " +
                "WHERE e.`user_id` = @userId LIMIT 1", new { userId });
        if (context == null)
        {
            session.SendWhisper("You don't work for a corporation.");
            return null;
        }
        if (context.RankOrder < context.ManageRankOrder)
        {
            session.SendWhisper("You're not senior enough to do that.");
            return null;
        }
        if (!ShiftManager.IsOnDuty(userId))
        {
            session.SendWhisper("You must be on duty to do that.");
            return null;
        }
        return context;
    }

    /// <summary>
    /// pixelrp: re-broadcast every employee of a corporation (0 = all corps)
    /// hotel-wide. The trigger after corp-level DB edits (badge, name,
    /// acronym, rank names) so infostands, profiles and corp windows update
    /// in real-time. Returns the employee count synced.
    /// </summary>
    public static int BroadcastAllEmployments(int corpId = 0)
    {
        List<int> userIds;
        using (var connection = PlusEnvironment.DatabaseManager.Connection())
            userIds = connection.Query<int>(
                "SELECT `user_id` FROM `rp_corporation_employees`" + (corpId > 0 ? " WHERE `corporation_id` = @corpId" : ""),
                new { corpId }).ToList();
        foreach (var userId in userIds)
            BroadcastEmployment(userId);
        return userIds.Count;
    }

    /// <summary>
    /// pixelrp: the one call every employment mutation (hire, fire, future
    /// promotions) makes - hotel-wide broadcast so every open Corporations
    /// window, profile and infostand updates in real-time, plus a live-shift
    /// refresh so an on-duty player's wage and working motto follow.
    /// </summary>
    public static void BroadcastEmployment(int userId)
    {
        var employment = GetEmployment(userId);
        PlusEnvironment.Game.ClientManager.SendPacket(ComposeFor(userId, employment));
        ShiftManager.RefreshSession(userId);
    }

    public static RpUserCorpComposer ComposeFor(int userId, Employment employment)
    {
        if (employment == null || employment.CorpId == 0)
            return new RpUserCorpComposer(userId, 0, "", "", "", 0, 0, 0, false);
        var live = ShiftManager.LiveSessionSeconds(userId);
        return new RpUserCorpComposer(employment.UserId, employment.CorpId, employment.Badge, employment.CorpName, employment.RankName, employment.Tier,
            employment.ShiftSeconds + live, employment.ShiftSecondsWeek + live, ShiftManager.IsOnDuty(userId));
    }
}

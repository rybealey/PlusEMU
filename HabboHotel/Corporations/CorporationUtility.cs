using Dapper;
using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Rooms;
using Plus.HabboHotel.Users;
using Plus.Communication.Packets.Outgoing.Users;
using Plus.Communication.Packets.Outgoing.Rooms.Settings;

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

    // pixelrp: assembles the RpRoomCorpComposer for a room - its HQ corp's
    // ranks with per-rank authorization plus the room's emergency flags.
    public static RpRoomCorpComposer BuildRoomCorp(Room room)
    {
        var ranks = new List<RpRoomCorpComposer.RankRow>();
        if (room.CorporationId > 0)
        {
            using var connection = PlusEnvironment.DatabaseManager.Connection();
            var rows = connection.Query<(int Id, int RankOrder, string Name, int Authorized)>(
                "SELECT r.`id` AS Id, r.`rank_order` AS RankOrder, r.`name` AS Name, " +
                "(a.`rank_id` IS NOT NULL) AS Authorized " +
                "FROM `rp_corporation_ranks` r " +
                "LEFT JOIN `rp_hq_room_ranks` a ON a.`rank_id` = r.`id` AND a.`room_id` = @roomId " +
                "WHERE r.`corporation_id` = @corpId ORDER BY r.`rank_order`",
                new { roomId = room.Id, corpId = room.CorporationId });
            foreach (var row in rows)
                ranks.Add(new RpRoomCorpComposer.RankRow(row.Id, row.RankOrder, row.Name, row.Authorized == 1));
        }
        return new RpRoomCorpComposer((int)room.Id, room.CorporationId, ranks,
            room.AllowMedical, room.AllowPolice, room.AllowStaff);
    }

    // pixelrp: may this employee be on the clock in the room they're
    // standing in right now? `isStart` = clocking in (:startwork) vs the
    // per-minute continue check.
    //   - City Government (service 'staff'): works and starts anywhere.
    //   - Their own corp's HQ: rank must be authorized (start + continue).
    //   - Emergency service (Medical/Police): may only CONTINUE (never start)
    //     in a room that admits their service and isn't their HQ, and only
    //     for eligible ranks (Police any rank, Medical Paramedic and above -
    //     rp_corporation_ranks.emergency_eligible). So they clock in at their
    //     own HQ, then may keep working a scene elsewhere.
    //   - A room that is nobody's HQ: their corp has no HQ at all → work
    //     anywhere (rollout fallback). Another corp's HQ is otherwise
    //     exclusive - the no-HQ fallback does not open it.
    // Reason is whisper-ready.
    public static (bool Ok, string Reason) EvaluateWork(Habbo habbo, bool isStart)
    {
        var room = habbo?.CurrentRoom;
        using var connection = PlusEnvironment.DatabaseManager.Connection();
        var job = connection.QuerySingleOrDefault<(int CorpId, int RankId, string ServiceType, int EmergencyEligible)?>(
            "SELECT e.`corporation_id` AS CorpId, e.`rank_id` AS RankId, c.`service_type` AS ServiceType, " +
            "r.`emergency_eligible` AS EmergencyEligible " +
            "FROM `rp_corporation_employees` e " +
            "INNER JOIN `rp_corporations` c ON c.`id` = e.`corporation_id` " +
            "INNER JOIN `rp_corporation_ranks` r ON r.`id` = e.`rank_id` " +
            "WHERE e.`user_id` = @userId LIMIT 1", new { userId = habbo.Id });
        if (job == null) return (false, "You don't have a job. Get hired by a corporation first.");

        if (room == null) return (false, "You can only work at your headquarters or an approved location.");

        // City Government (staff service) works and starts anywhere.
        if (job.Value.ServiceType == "staff") return (true, "");

        // At their own corp's HQ: rank authorization is definitive.
        if (room.CorporationId == job.Value.CorpId)
        {
            var authorized = connection.QuerySingleOrDefault<int?>(
                "SELECT 1 FROM `rp_hq_room_ranks` WHERE `room_id` = @roomId AND `rank_id` = @rankId LIMIT 1",
                new { roomId = room.Id, rankId = job.Value.RankId });
            if (authorized != null) return (true, "");
            return (false, "Your rank isn't cleared to work here.");
        }

        // Emergency service (Medical/Police): CONTINUE only, never start, and
        // only for eligible ranks.
        if (!isStart && job.Value.EmergencyEligible == 1)
        {
            var svc = job.Value.ServiceType;
            if ((svc == "medical" && room.AllowMedical) ||
                (svc == "police" && room.AllowPolice))
                return (true, "");
        }

        // Another corp's headquarters, not admitted here: excluded, even if
        // their own corp has no HQ.
        if (room.CorporationId != 0)
            return (false, "You can only work at your headquarters or an approved location.");

        // A room that is nobody's HQ: corps with no HQ of their own work
        // anywhere (the rollout fallback).
        var hqCount = connection.QuerySingle<int>(
            "SELECT COUNT(*) FROM `rooms` WHERE `corporation_id` = @corpId", new { corpId = job.Value.CorpId });
        if (hqCount == 0) return (true, "");

        // A corp WITH an HQ can only work at its HQ or (mid-shift) emergency rooms.
        return (false, "You can only work at your headquarters or an approved location.");
    }
}

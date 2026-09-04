using Dapper;
using Plus.Communication.Packets.Outgoing.Users;

namespace Plus.HabboHotel.Gangs;

/// <summary>
/// pixelrp: gangs ARE groups flagged is_gang = '1' (see the parent repo's
/// docs/superpowers/specs/2026-09-04-gangs-on-groups-design.md). Unlike
/// stock groups, a gang's colour1/colour2 hold RAW RGB ints picked in the
/// Gang window (GroupManager.GetColourCode never sees gangs), so turf furni
/// can later tint from the exact chosen colors. This mirrors
/// CorporationUtility: one membership query, one composer, one hotel-wide
/// broadcast that every gang mutation calls.
/// </summary>
public static class GangUtility
{
    // Property class, not a positional record: Dapper binds record
    // constructors only when every column type matches exactly, and MySQL
    // hands back groups.id / owner_id as UNSIGNED (uint) and the ownership
    // comparison as BIGINT (long). Settable properties get type conversion.
    public class GangMembership
    {
        public int GangId { get; set; }
        public string Name { get; set; } = "";
        public int Colour1 { get; set; }
        public int Colour2 { get; set; }
        public int IsOwner { get; set; }
    }

    public static GangMembership GetGang(int userId)
    {
        using var connection = PlusEnvironment.DatabaseManager.Connection();
        return connection.QueryFirstOrDefault<GangMembership>(
            "SELECT g.`id` AS GangId, g.`name` AS Name, g.`colour1` AS Colour1, g.`colour2` AS Colour2, (g.`owner_id` = @userId) AS IsOwner " +
            "FROM `group_memberships` m " +
            "INNER JOIN `groups` g ON g.`id` = m.`group_id` " +
            "WHERE m.`user_id` = @userId AND g.`is_gang` = '1' LIMIT 1", new { userId });
    }

    public static bool GangNameTaken(string name)
    {
        using var connection = PlusEnvironment.DatabaseManager.Connection();
        return connection.QueryFirstOrDefault<int?>(
            "SELECT `id` FROM `groups` WHERE `is_gang` = '1' AND `name` = @name LIMIT 1", new { name }) != null;
    }

    public static RpUserGangComposer ComposeFor(int userId, GangMembership gang)
    {
        var cost = GangCost();
        if (gang == null)
            return new RpUserGangComposer(userId, 0, "", "", "", false, cost);
        return new RpUserGangComposer(userId, gang.GangId, gang.Name, ToHex(gang.Colour1), ToHex(gang.Colour2), gang.IsOwner == 1, cost);
    }

    private static string ToHex(int colour) => $"#{colour & 0xFFFFFF:x6}";

    /// <summary>
    /// pixelrp: gang creation price in credits. Missing/zero setting falls
    /// back to 500 rather than a free gang (SettingsManager returns "0" for
    /// unknown keys).
    /// </summary>
    public static int GangCost()
    {
        return int.TryParse(PlusEnvironment.SettingsManager.TryGetValue("gang.cost"), out var cost) && cost > 0 ? cost : 500;
    }

    /// <summary>
    /// pixelrp: the one call every gang mutation (create, future join/leave/
    /// disband) makes - hotel-wide broadcast, same shape as
    /// CorporationUtility.BroadcastEmployment, so open profiles and the Gang
    /// window update in real-time without a re-request.
    /// </summary>
    public static void BroadcastGangMembership(int userId)
    {
        PlusEnvironment.Game.ClientManager.SendPacket(ComposeFor(userId, GetGang(userId)));
    }
}

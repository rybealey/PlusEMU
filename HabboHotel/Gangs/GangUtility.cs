using System.Collections.Concurrent;
using Dapper;
using Plus.Communication.Packets.Outgoing.Users;
using Plus.Utilities;

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
        // the member's role in the gang ("Member", or whatever the owner named
        // it) - what the profile's gang card shows; "" if they somehow have none
        public string RoleName { get; set; } = "";
    }

    // pixelrp (Twist, 2026-10-27): memberships as last read, for what only
    // SHOWS a player's gang or is asked again and again - the HUD target's gang
    // card, :ga, the turf panel and its contest, :heal's same-gang check, the
    // vest's colour - each of which read the database every time, on that
    // player's own packet thread or the room's tick. What DECIDES by a gang
    // (founding, inviting, joining, a claim, the gang shop) still reads GetGang.
    // In-game changes refresh it at once: a join, leave, kick or disband goes
    // through BroadcastGangMembership, and a rename, recolour, role edit or new
    // owner through GangManager.BroadcastDetail (ForgetGang). Housekeeping
    // writes straight to the database, so an entry is read again once it is
    // MembershipFreshMs old whatever happens. The generation stops a read that
    // raced a change from storing what it read before it (PartnershipUtility).
    private static readonly ConcurrentDictionary<int, (GangMembership? Gang, long ReadAt)> Memberships = new();
    private static readonly object MembershipsLock = new();
    private static long _membershipsGeneration;
    private const long MembershipFreshMs = 30_000;

    /// <summary>
    /// pixelrp (Twist, 2026-10-28): this player's gang as last read, only when
    /// that is fresh enough to show (<see cref="GetGangShown"/>) - false when it
    /// would have to be read from the database.
    /// </summary>
    public static bool TryGetGangShown(int userId, out GangMembership? gang)
    {
        gang = null;
        if (!Memberships.TryGetValue(userId, out var known) || (Environment.TickCount64 - known.ReadAt) >= MembershipFreshMs)
            return false;
        gang = known.Gang;
        return true;
    }

    /// <summary>This player's gang as last read - see <see cref="Memberships"/>. Null for none.</summary>
    public static GangMembership? GetGangShown(int userId)
    {
        if (Memberships.TryGetValue(userId, out var known) && (Environment.TickCount64 - known.ReadAt) < MembershipFreshMs)
            return known.Gang;
        var generation = Interlocked.Read(ref _membershipsGeneration);
        var gang = GetGang(userId);
        lock (MembershipsLock)
        {
            if (Interlocked.Read(ref _membershipsGeneration) == generation)
                Memberships[userId] = (gang, Environment.TickCount64);
        }
        StaleEntries.Sweep(Memberships, (_, entry) => (Environment.TickCount64 - entry.ReadAt) >= MembershipFreshMs);
        return gang;
    }

    /// <summary>
    /// pixelrp (Twist, 2026-10-28): read every one of these players whose gang
    /// is not fresh in <see cref="Memberships"/> in ONE query, so the
    /// <see cref="GetGangShown"/> calls straight after find them all in memory.
    /// For the turf panel, which asks for everyone in the room on the room's
    /// own tick - one query per player, a room's worth at once.
    /// </summary>
    public static void WarmGangsShown(IEnumerable<int> userIds)
    {
        var now = Environment.TickCount64;
        var missing = userIds
            .Where(id => id > 0 && !(Memberships.TryGetValue(id, out var known) && (now - known.ReadAt) < MembershipFreshMs))
            .Distinct()
            .ToList();
        if (missing.Count == 0)
            return;
        var generation = Interlocked.Read(ref _membershipsGeneration);
        var found = new Dictionary<int, GangMembership>();
        using (var connection = PlusEnvironment.DatabaseManager.Connection())
        {
            // GetGang's query for many players at once; the first row per
            // player, as its LIMIT 1 takes.
            foreach (var row in connection.Query<GangMembershipOf>(
                         "SELECT m.`user_id` AS UserId, g.`id` AS GangId, g.`name` AS Name, g.`colour1` AS Colour1, g.`colour2` AS Colour2, (g.`owner_id` = m.`user_id`) AS IsOwner, COALESCE(r.`name`, '') AS RoleName " +
                         "FROM `group_memberships` m " +
                         "INNER JOIN `groups` g ON g.`id` = m.`group_id` " +
                         "LEFT JOIN `rp_gang_members` s ON s.`user_id` = m.`user_id` AND s.`gang_id` = m.`group_id` " +
                         "LEFT JOIN `rp_gang_roles` r ON r.`id` = s.`role_id` " +
                         "WHERE m.`user_id` IN @missing AND g.`is_gang` = '1'", new { missing }))
                found.TryAdd(row.UserId, row);
        }
        lock (MembershipsLock)
        {
            // A change landed while this read: leave them to be read one by one.
            if (Interlocked.Read(ref _membershipsGeneration) != generation)
                return;
            var readAt = Environment.TickCount64;
            foreach (var userId in missing)
                Memberships[userId] = (found.GetValueOrDefault(userId), readAt);
        }
    }

    /// <summary>A membership and whose it is - for <see cref="WarmGangsShown"/>.</summary>
    private class GangMembershipOf : GangMembership
    {
        public int UserId { get; set; }
    }

    /// <summary>This player's gang has just been read after a change: keep it.</summary>
    private static void RememberGang(int userId, GangMembership? gang)
    {
        lock (MembershipsLock)
        {
            Interlocked.Increment(ref _membershipsGeneration);
            Memberships[userId] = (gang, Environment.TickCount64);
        }
    }

    /// <summary>A gang's name, colours, roles or owner changed: its members are read again.</summary>
    public static void ForgetGang(int gangId)
    {
        lock (MembershipsLock)
        {
            Interlocked.Increment(ref _membershipsGeneration);
            foreach (var (userId, entry) in Memberships)
            {
                if (entry.Gang?.GangId == gangId)
                    Memberships.TryRemove(userId, out _);
            }
        }
    }

    public static GangMembership GetGang(int userId)
    {
        using var connection = PlusEnvironment.DatabaseManager.Connection();
        return connection.QueryFirstOrDefault<GangMembership>(
            "SELECT g.`id` AS GangId, g.`name` AS Name, g.`colour1` AS Colour1, g.`colour2` AS Colour2, (g.`owner_id` = @userId) AS IsOwner, COALESCE(r.`name`, '') AS RoleName " +
            "FROM `group_memberships` m " +
            "INNER JOIN `groups` g ON g.`id` = m.`group_id` " +
            "LEFT JOIN `rp_gang_members` s ON s.`user_id` = m.`user_id` AND s.`gang_id` = m.`group_id` " +
            "LEFT JOIN `rp_gang_roles` r ON r.`id` = s.`role_id` " +
            "WHERE m.`user_id` = @userId AND g.`is_gang` = '1' LIMIT 1", new { userId });
    }

    public static bool GangNameTaken(string name)
    {
        using var connection = PlusEnvironment.DatabaseManager.Connection();
        return connection.QueryFirstOrDefault<int?>(
            "SELECT `id` FROM `groups` WHERE `is_gang` = '1' AND `name` = @name LIMIT 1", new { name }) != null;
    }

    public static RpUserGangComposer ComposeFor(int userId, GangMembership? gang)
    {
        var cost = GangCost();
        if (gang == null)
            return new RpUserGangComposer(userId, 0, "", "", "", false, cost);
        return new RpUserGangComposer(userId, gang.GangId, gang.Name, ToHex(gang.Colour1), ToHex(gang.Colour2), gang.IsOwner == 1, cost, gang.RoleName);
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
        var gang = GetGang(userId);
        RememberGang(userId, gang);
        PlusEnvironment.Game.ClientManager.SendPacket(ComposeFor(userId, gang));
        // pixelrp turfs: the one place every gang change passes through, so a
        // player who founds, joins or leaves a gang while standing in a turf
        // sees the panel's button change without re-entering the room.
        TurfManager.OnMembershipChanged(userId, gang?.GangId ?? 0);
        // pixelrp body armour: the vest is in the gang's primary colour, so it
        // changes with the gang - or goes back to grey without one.
        Users.RpDurability.OnGangChanged(userId, gang?.Colour1);
    }
}

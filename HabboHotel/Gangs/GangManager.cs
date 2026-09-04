using Dapper;
using Plus.Communication.Packets.Outgoing.Moderation;
using Plus.Communication.Packets.Outgoing.Users;
using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Groups;
using Plus.Utilities;

namespace Plus.HabboHotel.Gangs;

/// <summary>
/// pixelrp: gang roster, roles and invites (slice 3 of gangs-on-groups).
/// Membership itself stays canonical in group_memberships (GangUtility
/// answers "is this player in a gang"); rp_gang_members / rp_gang_roles /
/// rp_gang_invites are the gang-only sidecars. Every mutation ends in
/// BroadcastDetail(gangId) so each online member's Gang window redraws with
/// ITS OWN permissions, and in GangUtility.BroadcastGangMembership for any
/// player whose membership changed.
/// </summary>
public static class GangManager
{
    public const int PermInvite = 1;
    public const int PermKick = 2;
    public const int PermBank = 4;
    public const int PermAdmin = 8;
    public const int PermLeader = 16;
    public const int PermAll = PermInvite | PermKick | PermBank | PermAdmin | PermLeader;
    // the flags a role row may carry (leader is never a role)
    public const int RoleFlagMask = PermInvite | PermKick | PermBank | PermAdmin;

    public const int MaxRoleNameLength = 29;
    public const int MaxRoles = 12;
    public const int MaxPendingInvites = 20;

    public record GangRow(int Id, string Name, int Colour1, int Colour2, int OwnerId, int Created, int GangLevel, int GangXp);
    public record RoleRow(int Id, string Name, int SortOrder, int CanInvite, int CanKick, int CanBank, int IsAdmin);
    public record MemberRow(int UserId, string Username, string Figure, int? RoleId, int JoinedAt);
    public record InviteRow(int UserId, string Username, string Figure, int InvitedBy, string InviterName, int ExpiresAt);
    public record IncomingInviteRow(int GangId, string Name, int Colour1, int Colour2, string InviterName, int ExpiresAt);

    public record Snapshot(GangRow Gang, List<RoleRow> Roles, List<MemberRow> Members, List<InviteRow> Invites);

    public record Actor(int UserId, Snapshot Snapshot, int Permissions)
    {
        public int GangId => Snapshot.Gang.Id;
        public bool IsLeader => (Permissions & PermLeader) != 0;
    }

    public static int Now() => (int)UnixTimestamp.GetNow();

    public static string ToHex(int colour) => $"#{colour & 0xFFFFFF:x6}";

    /// <summary>Hours a pending invite lives; server_settings gang.invite.hours, default 24.</summary>
    public static int InviteHours()
    {
        return int.TryParse(PlusEnvironment.SettingsManager.TryGetValue("gang.invite.hours"), out var hours) && hours > 0 ? hours : 24;
    }

    /// <summary>XP needed to clear a level. Nothing awards gang XP yet; the bar is wired for when turfs do.</summary>
    public static int XpCap(int level) => Math.Max(1, level) * 200;

    public static int RoleFlags(RoleRow role)
    {
        if (role == null)
            return 0;
        var flags = 0;
        if (role.CanInvite == 1) flags |= PermInvite;
        if (role.CanKick == 1) flags |= PermKick;
        if (role.CanBank == 1) flags |= PermBank;
        if (role.IsAdmin == 1) flags |= PermAdmin | PermInvite | PermKick;
        return flags;
    }

    public static GangRow GetGang(int gangId)
    {
        using var connection = PlusEnvironment.DatabaseManager.Connection();
        return connection.QueryFirstOrDefault<GangRow>(
            "SELECT `id` AS Id, `name` AS Name, `colour1` AS Colour1, `colour2` AS Colour2, `owner_id` AS OwnerId, `created` AS Created, " +
            "`gang_level` AS GangLevel, `gang_xp` AS GangXp FROM `groups` WHERE `id` = @gangId AND `is_gang` = '1' LIMIT 1", new { gangId });
    }

    public static List<RoleRow> GetRoles(int gangId)
    {
        using var connection = PlusEnvironment.DatabaseManager.Connection();
        return connection.Query<RoleRow>(
            "SELECT `id` AS Id, `name` AS Name, `sort_order` AS SortOrder, (`can_invite` = '1') AS CanInvite, (`can_kick` = '1') AS CanKick, " +
            "(`can_bank` = '1') AS CanBank, (`is_admin` = '1') AS IsAdmin FROM `rp_gang_roles` WHERE `gang_id` = @gangId ORDER BY `sort_order`, `id`",
            new { gangId }).ToList();
    }

    public static List<MemberRow> GetMembers(int gangId)
    {
        using var connection = PlusEnvironment.DatabaseManager.Connection();
        return connection.Query<MemberRow>(
            "SELECT m.`user_id` AS UserId, u.`username` AS Username, u.`look` AS Figure, s.`role_id` AS RoleId, COALESCE(s.`joined_at`, g.`created`) AS JoinedAt " +
            "FROM `group_memberships` m " +
            "INNER JOIN `groups` g ON g.`id` = m.`group_id` " +
            "INNER JOIN `users` u ON u.`id` = m.`user_id` " +
            "LEFT JOIN `rp_gang_members` s ON s.`user_id` = m.`user_id` AND s.`gang_id` = m.`group_id` " +
            "WHERE m.`group_id` = @gangId ORDER BY u.`username`", new { gangId }).ToList();
    }

    public static void PurgeExpiredInvites()
    {
        using var connection = PlusEnvironment.DatabaseManager.Connection();
        connection.Execute("DELETE FROM `rp_gang_invites` WHERE `expires_at` <= @now", new { now = Now() });
    }

    public static List<InviteRow> GetInvites(int gangId)
    {
        PurgeExpiredInvites();
        using var connection = PlusEnvironment.DatabaseManager.Connection();
        return connection.Query<InviteRow>(
            "SELECT i.`user_id` AS UserId, u.`username` AS Username, u.`look` AS Figure, i.`invited_by` AS InvitedBy, " +
            "COALESCE(b.`username`, '') AS InviterName, i.`expires_at` AS ExpiresAt " +
            "FROM `rp_gang_invites` i " +
            "INNER JOIN `users` u ON u.`id` = i.`user_id` " +
            "LEFT JOIN `users` b ON b.`id` = i.`invited_by` " +
            "WHERE i.`gang_id` = @gangId ORDER BY i.`expires_at`", new { gangId }).ToList();
    }

    /// <summary>Invites waiting on one player (who is not in a gang).</summary>
    public static List<IncomingInviteRow> GetIncomingInvites(int userId)
    {
        PurgeExpiredInvites();
        using var connection = PlusEnvironment.DatabaseManager.Connection();
        return connection.Query<IncomingInviteRow>(
            "SELECT g.`id` AS GangId, g.`name` AS Name, g.`colour1` AS Colour1, g.`colour2` AS Colour2, COALESCE(b.`username`, '') AS InviterName, i.`expires_at` AS ExpiresAt " +
            "FROM `rp_gang_invites` i " +
            "INNER JOIN `groups` g ON g.`id` = i.`gang_id` AND g.`is_gang` = '1' " +
            "LEFT JOIN `users` b ON b.`id` = i.`invited_by` " +
            "WHERE i.`user_id` = @userId ORDER BY i.`expires_at`", new { userId }).ToList();
    }

    public static Snapshot Load(int gangId)
    {
        var gang = GetGang(gangId);
        if (gang == null)
            return null;
        return new Snapshot(gang, GetRoles(gangId), GetMembers(gangId), GetInvites(gangId));
    }

    public static int PermissionsOf(Snapshot snapshot, int userId)
    {
        if (snapshot == null)
            return 0;
        if (snapshot.Gang.OwnerId == userId)
            return PermAll;
        var member = snapshot.Members.FirstOrDefault(row => row.UserId == userId);
        if (member == null || !member.RoleId.HasValue)
            return 0;
        return RoleFlags(snapshot.Roles.FirstOrDefault(role => role.Id == member.RoleId.Value));
    }

    public static RpGangDetailComposer Compose(Snapshot snapshot, int forUserId)
    {
        var gang = snapshot.Gang;
        var permissions = PermissionsOf(snapshot, forUserId);
        var ownerName = snapshot.Members.FirstOrDefault(row => row.UserId == gang.OwnerId)?.Username ?? PlusEnvironment.GetUsernameById(gang.OwnerId);
        var roles = snapshot.Roles
            .Select(role => new RpGangDetailComposer.Role(role.Id, role.Name, role.SortOrder, RoleFlags(role)))
            .ToList();
        var members = snapshot.Members
            .Select(member => new RpGangDetailComposer.Member(member.UserId, member.Username, member.Figure ?? "", member.RoleId ?? 0,
                PlusEnvironment.Game.ClientManager.GetClientByUserId(member.UserId) != null, member.JoinedAt))
            .ToList();
        // pending invites are only shown to players who can act on them
        var invites = (permissions & PermInvite) != 0
            ? snapshot.Invites.Select(invite => new RpGangDetailComposer.Invite(invite.UserId, invite.Username, invite.Figure ?? "", invite.InviterName, invite.ExpiresAt)).ToList()
            : new List<RpGangDetailComposer.Invite>();
        return new RpGangDetailComposer(gang.Id, gang.Name, ToHex(gang.Colour1), ToHex(gang.Colour2), gang.OwnerId, ownerName ?? "",
            gang.GangLevel, gang.GangXp, XpCap(gang.GangLevel), gang.Created, permissions, roles, members, invites, InviteHours());
    }

    public static RpGangInvitesComposer ComposeIncomingInvites(int userId)
    {
        var rows = GetIncomingInvites(userId)
            .Select(row => new RpGangInvitesComposer.Invite(row.GangId, row.Name, ToHex(row.Colour1), ToHex(row.Colour2), row.InviterName, row.ExpiresAt))
            .ToList();
        return new RpGangInvitesComposer(rows);
    }

    /// <summary>
    /// The Gang window's state packet: the full detail when the player is in
    /// a gang, otherwise the invites waiting on them (drives the create view's
    /// invite banner).
    /// </summary>
    public static void SendState(GameClient session)
    {
        var habbo = session.GetHabbo();
        if (habbo == null)
            return;
        var membership = GangUtility.GetGang(habbo.Id);
        if (membership != null)
        {
            var snapshot = Load(membership.GangId);
            if (snapshot != null)
            {
                session.Send(Compose(snapshot, habbo.Id));
                return;
            }
        }
        session.Send(ComposeIncomingInvites(habbo.Id));
    }

    /// <summary>Every online member gets the detail composed for THEIR permissions.</summary>
    public static void BroadcastDetail(int gangId)
    {
        var snapshot = Load(gangId);
        if (snapshot == null)
            return;
        foreach (var member in snapshot.Members)
        {
            var client = PlusEnvironment.Game.ClientManager.GetClientByUserId(member.UserId);
            if (client?.GetHabbo() != null)
                client.Send(Compose(snapshot, member.UserId));
        }
    }

    /// <summary>Refresh an online, gang-less player's incoming invites (no-op when offline or in a gang).</summary>
    public static void SendIncomingInvites(int userId)
    {
        var client = PlusEnvironment.Game.ClientManager.GetClientByUserId(userId);
        if (client?.GetHabbo() == null || GangUtility.GetGang(userId) != null)
            return;
        client.Send(ComposeIncomingInvites(userId));
    }

    public static void Alert(int userId, string message)
    {
        var client = PlusEnvironment.Game.ClientManager.GetClientByUserId(userId);
        if (client?.GetHabbo() != null)
            client.Send(new BroadcastMessageAlertComposer(message));
    }

    /// <summary>
    /// The gate every management packet passes through: the actor must be in
    /// a gang and hold at least one of the required permission bits (0 = any
    /// member). Whispers the reason and returns null when it fails.
    /// </summary>
    public static Actor GetActor(GameClient session, int required)
    {
        var habbo = session.GetHabbo();
        if (habbo == null)
            return null;
        var membership = GangUtility.GetGang(habbo.Id);
        if (membership == null)
        {
            session.SendWhisper("You're not in a gang.");
            return null;
        }
        var snapshot = Load(membership.GangId);
        if (snapshot == null)
            return null;
        var permissions = PermissionsOf(snapshot, habbo.Id);
        if (required != 0 && (permissions & required) == 0)
        {
            session.SendWhisper("You don't have permission to do that in your gang.");
            return null;
        }
        return new Actor(habbo.Id, snapshot, permissions);
    }

    /// <summary>Sidecar row for a member (founder or accepted invite); membership itself is written by the caller/group.</summary>
    public static void WriteMemberRow(int gangId, int userId)
    {
        using var connection = PlusEnvironment.DatabaseManager.Connection();
        connection.Execute(
            "INSERT INTO `rp_gang_members` (`gang_id`, `user_id`, `role_id`, `joined_at`) VALUES (@gangId, @userId, NULL, @now) " +
            "ON DUPLICATE KEY UPDATE `gang_id` = VALUES(`gang_id`), `role_id` = NULL, `joined_at` = VALUES(`joined_at`)",
            new { gangId, userId, now = Now() });
    }

    public static void AddMember(IGroupManager groupManager, int gangId, int userId)
    {
        if (groupManager.TryGetGroup(gangId, out var group))
        {
            if (!group.IsMember(userId))
                group.AddMember(userId);
        }
        else
        {
            using var connection = PlusEnvironment.DatabaseManager.Connection();
            connection.Execute("INSERT INTO `group_memberships` (`user_id`, `group_id`) VALUES (@userId, @gangId)", new { userId, gangId });
        }
        WriteMemberRow(gangId, userId);
        using (var connection = PlusEnvironment.DatabaseManager.Connection())
        {
            // joining one gang voids every other invite they were holding
            connection.Execute("DELETE FROM `rp_gang_invites` WHERE `user_id` = @userId", new { userId });
        }
        GangUtility.BroadcastGangMembership(userId);
    }

    public static void RemoveMember(IGroupManager groupManager, int gangId, int userId)
    {
        if (groupManager.TryGetGroup(gangId, out var group))
        {
            if (group.IsAdmin(userId))
                group.TakeAdmin(userId);
            group.DeleteMember(userId);
        }
        using (var connection = PlusEnvironment.DatabaseManager.Connection())
        {
            connection.Execute("DELETE FROM `group_memberships` WHERE `group_id` = @gangId AND `user_id` = @userId", new { gangId, userId });
            connection.Execute("DELETE FROM `rp_gang_members` WHERE `gang_id` = @gangId AND `user_id` = @userId", new { gangId, userId });
        }
        GangUtility.BroadcastGangMembership(userId);
    }

    /// <summary>The leader tears the gang down: every member is freed, invites void, the group row goes.</summary>
    public static void Disband(IGroupManager groupManager, int gangId)
    {
        var memberIds = GetMembers(gangId).Select(row => row.UserId).ToList();
        List<int> inviteeIds;
        using (var connection = PlusEnvironment.DatabaseManager.Connection())
        {
            inviteeIds = connection.Query<int>("SELECT `user_id` FROM `rp_gang_invites` WHERE `gang_id` = @gangId", new { gangId }).ToList();
            connection.Execute("DELETE FROM `group_memberships` WHERE `group_id` = @gangId", new { gangId });
            connection.Execute("DELETE FROM `rp_gang_members` WHERE `gang_id` = @gangId", new { gangId });
            connection.Execute("DELETE FROM `rp_gang_roles` WHERE `gang_id` = @gangId", new { gangId });
            connection.Execute("DELETE FROM `rp_gang_invites` WHERE `gang_id` = @gangId", new { gangId });
            connection.Execute("DELETE FROM `groups` WHERE `id` = @gangId AND `is_gang` = '1'", new { gangId });
        }
        groupManager.DeleteGroup(gangId);
        foreach (var userId in memberIds)
            GangUtility.BroadcastGangMembership(userId);
        foreach (var userId in inviteeIds)
            SendIncomingInvites(userId);
    }
}

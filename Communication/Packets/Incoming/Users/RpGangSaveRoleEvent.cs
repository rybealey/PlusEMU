using Dapper;
using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Gangs;
using Plus.HabboHotel.Rooms.Chat.Filter;

namespace Plus.Communication.Packets.Incoming.Users;

/// <summary>
/// pixelrp: create (roleId 0) or edit a custom role - name plus permission
/// flags (GangManager.RoleFlagMask). Requires Administrator; only the leader
/// may grant or edit Administrator.
/// </summary>
internal class RpGangSaveRoleEvent : IPacketEvent
{
    private readonly IWordFilterManager _wordFilterManager;

    public RpGangSaveRoleEvent(IWordFilterManager wordFilterManager)
    {
        _wordFilterManager = wordFilterManager;
    }

    public Task Parse(GameClient session, IIncomingPacket packet)
    {
        var roleId = packet.ReadInt();
        var name = _wordFilterManager.CheckMessage(packet.ReadString()).Trim();
        var flags = packet.ReadInt() & GangManager.RoleFlagMask;
        var actor = GangManager.GetActor(session, GangManager.PermAdmin);
        if (actor == null)
            return Task.CompletedTask;

        if (name.Length == 0 || name.Length > GangManager.MaxRoleNameLength)
        {
            session.SendWhisper($"Role names are 1 to {GangManager.MaxRoleNameLength} characters.");
            return Task.CompletedTask;
        }
        if (name.Equals("Leader", StringComparison.OrdinalIgnoreCase) || name.Equals("Member", StringComparison.OrdinalIgnoreCase))
        {
            session.SendWhisper("That role name is reserved.");
            return Task.CompletedTask;
        }
        var roles = actor.Snapshot.Roles;
        if (roles.Any(role => role.Id != roleId && role.Name.Equals(name, StringComparison.OrdinalIgnoreCase)))
        {
            session.SendWhisper($"Your gang already has a role named '{name}'.");
            return Task.CompletedTask;
        }
        if ((flags & GangManager.PermAdmin) != 0 && !actor.IsLeader)
        {
            session.SendWhisper("Only the leader can grant Administrator.");
            return Task.CompletedTask;
        }

        var canInvite = (flags & GangManager.PermInvite) != 0 ? "1" : "0";
        var canKick = (flags & GangManager.PermKick) != 0 ? "1" : "0";
        var canBank = (flags & GangManager.PermBank) != 0 ? "1" : "0";
        var isAdmin = (flags & GangManager.PermAdmin) != 0 ? "1" : "0";

        using (var connection = PlusEnvironment.DatabaseManager.Connection())
        {
            if (roleId == 0)
            {
                if (roles.Count >= GangManager.MaxRoles)
                {
                    session.SendWhisper($"A gang can have at most {GangManager.MaxRoles} roles.");
                    return Task.CompletedTask;
                }
                connection.Execute(
                    "INSERT INTO `rp_gang_roles` (`gang_id`, `name`, `sort_order`, `can_invite`, `can_kick`, `can_bank`, `is_admin`) " +
                    "VALUES (@gangId, @name, @sortOrder, @canInvite, @canKick, @canBank, @isAdmin)",
                    new { gangId = actor.GangId, name, sortOrder = roles.Any() ? roles.Max(role => role.SortOrder) + 1 : 0, canInvite, canKick, canBank, isAdmin });
            }
            else
            {
                var existing = roles.FirstOrDefault(role => role.Id == roleId);
                if (existing == null)
                    return Task.CompletedTask;
                if (!actor.IsLeader && (GangManager.RoleFlags(existing) & GangManager.PermAdmin) != 0)
                {
                    session.SendWhisper("Only the leader can edit an administrator role.");
                    return Task.CompletedTask;
                }
                connection.Execute(
                    "UPDATE `rp_gang_roles` SET `name` = @name, `can_invite` = @canInvite, `can_kick` = @canKick, `can_bank` = @canBank, `is_admin` = @isAdmin " +
                    "WHERE `id` = @roleId AND `gang_id` = @gangId",
                    new { gangId = actor.GangId, roleId, name, canInvite, canKick, canBank, isAdmin });
            }
        }

        GangManager.BroadcastDetail(actor.GangId);
        return Task.CompletedTask;
    }
}

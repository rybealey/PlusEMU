using Dapper;
using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Gangs;

namespace Plus.Communication.Packets.Incoming.Users;

/// <summary>
/// pixelrp: give a member a role (roleId 0 = plain Member). Requires
/// Administrator; the leader's role is fixed, and only the leader can move
/// anyone into or out of an administrator role.
/// </summary>
internal class RpGangSetMemberRoleEvent : IPacketEvent
{
    public Task Parse(GameClient session, IIncomingPacket packet)
    {
        var userId = packet.ReadInt();
        var roleId = packet.ReadInt();
        var actor = GangManager.GetActor(session, GangManager.PermAdmin);
        if (actor == null)
            return Task.CompletedTask;

        if (actor.Snapshot.Members.All(member => member.UserId != userId))
        {
            session.SendWhisper("That player isn't in your gang.");
            return Task.CompletedTask;
        }
        if (userId == actor.Snapshot.Gang.OwnerId)
        {
            session.SendWhisper("The leader's role can't be changed.");
            return Task.CompletedTask;
        }
        if (userId == actor.UserId && !actor.IsLeader)
        {
            session.SendWhisper("You can't change your own role.");
            return Task.CompletedTask;
        }
        var role = roleId == 0 ? null : actor.Snapshot.Roles.FirstOrDefault(row => row.Id == roleId);
        if (roleId != 0 && role == null)
            return Task.CompletedTask;
        var currentFlags = GangManager.PermissionsOf(actor.Snapshot, userId);
        var newFlags = GangManager.RoleFlags(role);
        if (!actor.IsLeader && ((currentFlags | newFlags) & GangManager.PermAdmin) != 0)
        {
            session.SendWhisper("Only the leader can assign or remove administrator roles.");
            return Task.CompletedTask;
        }

        using (var connection = PlusEnvironment.DatabaseManager.Connection())
        {
            connection.Execute(
                "INSERT INTO `rp_gang_members` (`gang_id`, `user_id`, `role_id`, `joined_at`) VALUES (@gangId, @userId, @roleId, @now) " +
                "ON DUPLICATE KEY UPDATE `role_id` = VALUES(`role_id`)",
                new { gangId = actor.GangId, userId, roleId = roleId == 0 ? (int?)null : roleId, now = GangManager.Now() });
        }
        GangManager.BroadcastDetail(actor.GangId);
        return Task.CompletedTask;
    }
}

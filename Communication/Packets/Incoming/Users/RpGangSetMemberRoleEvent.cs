using Dapper;
using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Gangs;

namespace Plus.Communication.Packets.Incoming.Users;

/// <summary>
/// pixelrp: move a member into one of the gang's roles - the Manage tab's
/// drag and its role dropdown. Requires Administrator. Anyone can be ranked
/// into any role, the owner included (ownership is not a role). Only the
/// owner may move themselves; an administrator can't change their own rank.
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
        if (userId == actor.UserId && !actor.IsOwner)
        {
            session.SendWhisper("You can't change your own role.");
            return Task.CompletedTask;
        }
        if (actor.Snapshot.Roles.All(role => role.Id != roleId))
            return Task.CompletedTask;

        using (var connection = PlusEnvironment.DatabaseManager.Connection())
        {
            connection.Execute(
                "INSERT INTO `rp_gang_members` (`gang_id`, `user_id`, `role_id`, `joined_at`) VALUES (@gangId, @userId, @roleId, @now) " +
                "ON DUPLICATE KEY UPDATE `role_id` = VALUES(`role_id`)",
                new { gangId = actor.GangId, userId, roleId, now = GangManager.Now() });
        }
        GangManager.BroadcastDetail(actor.GangId);
        return Task.CompletedTask;
    }
}

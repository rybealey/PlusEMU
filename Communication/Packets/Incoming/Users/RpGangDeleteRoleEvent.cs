using Dapper;
using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Gangs;

namespace Plus.Communication.Packets.Incoming.Users;

/// <summary>pixelrp: delete a custom role; its members drop to plain Member. Requires Administrator.</summary>
internal class RpGangDeleteRoleEvent : IPacketEvent
{
    public Task Parse(GameClient session, IIncomingPacket packet)
    {
        var roleId = packet.ReadInt();
        var actor = GangManager.GetActor(session, GangManager.PermAdmin);
        if (actor == null)
            return Task.CompletedTask;

        var role = actor.Snapshot.Roles.FirstOrDefault(row => row.Id == roleId);
        if (role == null)
            return Task.CompletedTask;
        if (!actor.IsLeader && (GangManager.RoleFlags(role) & GangManager.PermAdmin) != 0)
        {
            session.SendWhisper("Only the leader can delete an administrator role.");
            return Task.CompletedTask;
        }

        using (var connection = PlusEnvironment.DatabaseManager.Connection())
        {
            connection.Execute("UPDATE `rp_gang_members` SET `role_id` = NULL WHERE `gang_id` = @gangId AND `role_id` = @roleId", new { gangId = actor.GangId, roleId });
            connection.Execute("DELETE FROM `rp_gang_roles` WHERE `id` = @roleId AND `gang_id` = @gangId", new { gangId = actor.GangId, roleId });
        }
        GangManager.BroadcastDetail(actor.GangId);
        return Task.CompletedTask;
    }
}

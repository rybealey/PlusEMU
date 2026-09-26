using Dapper;
using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Gangs;

namespace Plus.Communication.Packets.Incoming.Users;

/// <summary>
/// pixelrp: delete a role. Requires Administrator. A gang always keeps at
/// least one role, so the last one is refused. The deleted role's members move
/// to the bottom of what remains - the same role a new member would join.
/// </summary>
internal class RpGangDeleteRoleEvent : IPacketEvent
{
    public Task Parse(GameClient session, IIncomingPacket packet)
    {
        var roleId = packet.ReadInt();
        var actor = GangManager.GetActor(session, GangManager.PermAdmin);
        if (actor == null)
            return Task.CompletedTask;

        var roles = actor.Snapshot.Roles;
        var role = roles.FirstOrDefault(row => row.Id == roleId);
        if (role == null)
            return Task.CompletedTask;
        if (roles.Count <= 1)
        {
            session.SendWhisper("A gang always keeps at least one role.");
            return Task.CompletedTask;
        }

        var fallback = roles
            .Where(row => row.Id != roleId)
            .OrderByDescending(row => row.SortOrder)
            .ThenByDescending(row => row.Id)
            .First();

        using (var connection = PlusEnvironment.DatabaseManager.Connection())
        {
            connection.Execute("UPDATE `rp_gang_members` SET `role_id` = @fallbackId WHERE `gang_id` = @gangId AND `role_id` = @roleId",
                new { gangId = actor.GangId, roleId, fallbackId = fallback.Id });
            connection.Execute("DELETE FROM `rp_gang_roles` WHERE `id` = @roleId AND `gang_id` = @gangId", new { gangId = actor.GangId, roleId });
        }
        GangManager.BroadcastDetail(actor.GangId);
        return Task.CompletedTask;
    }
}

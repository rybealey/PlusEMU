using Dapper;
using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Gangs;

namespace Plus.Communication.Packets.Incoming.Users;

/// <summary>
/// pixelrp: the roles list was drag-reordered - the ids arrive in their new
/// top-to-bottom order and become sort_order 0..n. Requires Administrator.
/// Ids that aren't this gang's roles are ignored.
/// </summary>
internal class RpGangReorderRolesEvent : IPacketEvent
{
    public Task Parse(GameClient session, IIncomingPacket packet)
    {
        var count = Math.Clamp(packet.ReadInt(), 0, GangManager.MaxRoles);
        var ids = new List<int>();
        for (var i = 0; i < count; i++)
            ids.Add(packet.ReadInt());
        var actor = GangManager.GetActor(session, GangManager.PermAdmin);
        if (actor == null || ids.Count == 0)
            return Task.CompletedTask;

        var known = actor.Snapshot.Roles.Select(role => role.Id).ToHashSet();
        var ordered = ids.Distinct().Where(known.Contains).ToList();
        using (var connection = PlusEnvironment.DatabaseManager.Connection())
        {
            for (var index = 0; index < ordered.Count; index++)
                connection.Execute("UPDATE `rp_gang_roles` SET `sort_order` = @order WHERE `id` = @id AND `gang_id` = @gangId",
                    new { order = index, id = ordered[index], gangId = actor.GangId });
        }
        GangManager.BroadcastDetail(actor.GangId);
        return Task.CompletedTask;
    }
}

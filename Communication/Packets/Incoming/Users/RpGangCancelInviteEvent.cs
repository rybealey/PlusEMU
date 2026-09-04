using Dapper;
using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Gangs;

namespace Plus.Communication.Packets.Incoming.Users;

/// <summary>pixelrp: revoke a pending invite (Invites tab). Requires the invite permission.</summary>
internal class RpGangCancelInviteEvent : IPacketEvent
{
    public Task Parse(GameClient session, IIncomingPacket packet)
    {
        var userId = packet.ReadInt();
        var actor = GangManager.GetActor(session, GangManager.PermInvite);
        if (actor == null || userId <= 0)
            return Task.CompletedTask;
        using (var connection = PlusEnvironment.DatabaseManager.Connection())
        {
            connection.Execute("DELETE FROM `rp_gang_invites` WHERE `gang_id` = @gangId AND `user_id` = @userId", new { gangId = actor.GangId, userId });
        }
        GangManager.BroadcastDetail(actor.GangId);
        GangManager.SendIncomingInvites(userId);
        return Task.CompletedTask;
    }
}

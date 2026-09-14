using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Sitch;

namespace Plus.Communication.Packets.Incoming.Users.Sitch;

/// <summary>pixelrp: repost or undo a repost. 1 = repost.</summary>
internal class RpSitchRepostEvent : IPacketEvent
{
    public Task Parse(GameClient session, IIncomingPacket packet)
    {
        var postId = packet.ReadInt();
        var on = packet.ReadInt() == 1;
        var habbo = session.GetHabbo();
        if (habbo == null || postId <= 0) return Task.CompletedTask;

        SitchUtility.SetRepost(postId, habbo.Id, on);
        SitchUtility.SendThread(session, postId);
        return Task.CompletedTask;
    }
}

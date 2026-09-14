using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Sitch;

namespace Plus.Communication.Packets.Incoming.Users.Sitch;

/// <summary>pixelrp: a Sitch post opened - the post and its replies.</summary>
internal class RpGetSitchThreadEvent : IPacketEvent
{
    public Task Parse(GameClient session, IIncomingPacket packet)
    {
        if (session.GetHabbo() == null) return Task.CompletedTask;
        var postId = packet.ReadInt();
        if (postId <= 0) return Task.CompletedTask;
        SitchUtility.SendThread(session, postId);
        return Task.CompletedTask;
    }
}

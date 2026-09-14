using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Sitch;

namespace Plus.Communication.Packets.Incoming.Users.Sitch;

/// <summary>pixelrp: Sitch opened, or a tab switched. 1 = Following, anything else = For you.</summary>
internal class RpGetSitchFeedEvent : IPacketEvent
{
    public Task Parse(GameClient session, IIncomingPacket packet)
    {
        if (session.GetHabbo() == null) return Task.CompletedTask;
        var following = packet.HasDataRemaining() && packet.ReadInt() == 1;
        SitchUtility.SendFeed(session, following);
        return Task.CompletedTask;
    }
}

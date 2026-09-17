using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Sitch;

namespace Plus.Communication.Packets.Incoming.Users.Sitch;

/// <summary>pixelrp: the Search tab opened - send it something to look at.</summary>
internal class RpGetSitchTrendingEvent : IPacketEvent
{
    public Task Parse(GameClient session, IIncomingPacket packet)
    {
        SitchUtility.SendTrending(session);
        return Task.CompletedTask;
    }
}

using Plus.Communication.Attributes;
using Plus.Communication.Packets.Outgoing.Marketplace;
using Plus.HabboHotel.GameClients;

namespace Plus.Communication.Packets.Incoming.Marketplace;

[StaffOnly]
internal class GetOwnOffersEvent : IPacketEvent
{
    public Task Parse(GameClient session, IIncomingPacket packet)
    {
        session.Send(new MarketPlaceOwnOffersComposer(session.GetHabbo().Id));
        return Task.CompletedTask;
    }
}
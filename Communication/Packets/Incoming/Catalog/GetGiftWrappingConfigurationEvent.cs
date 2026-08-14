using Plus.Communication.Attributes;
using Plus.Communication.Packets.Outgoing.Catalog;
using Plus.HabboHotel.GameClients;

namespace Plus.Communication.Packets.Incoming.Catalog;

[StaffOnly]
public class GetGiftWrappingConfigurationEvent : IPacketEvent
{
    public Task Parse(GameClient session, IIncomingPacket packet)
    {
        session.Send(new GiftWrappingConfigurationComposer());
        return Task.CompletedTask;
    }
}
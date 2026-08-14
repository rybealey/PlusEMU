using Plus.Communication.Attributes;
using Plus.Communication.Packets.Outgoing.Catalog;
using Plus.HabboHotel.GameClients;

namespace Plus.Communication.Packets.Incoming.Catalog;

[StaffOnly]
internal class GetClubGiftInfoEvent : IPacketEvent
{
    public Task Parse(GameClient session, IIncomingPacket packet)
    {
        session.Send(new ClubGiftsComposer());
        return Task.CompletedTask;
    }
}
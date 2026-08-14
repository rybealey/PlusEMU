using Plus.Communication.Attributes;
using Plus.Communication.Packets.Outgoing.Navigator;
using Plus.HabboHotel.GameClients;

namespace Plus.Communication.Packets.Incoming.Navigator;

[StaffOnly]
internal class CanCreateRoomEvent : IPacketEvent
{
    public Task Parse(GameClient session, IIncomingPacket packet)
    {
        session.Send(new CanCreateRoomComposer(false, 150));
        return Task.CompletedTask;
    }
}
using Plus.Communication.Attributes;
using Plus.Communication.Packets.Outgoing.Camera;
using Plus.HabboHotel.GameClients;

namespace Plus.Communication.Packets.Incoming.Camera;

[VipOnly]
internal class InitCameraEvent : IPacketEvent
{
    public Task Parse(GameClient session, IIncomingPacket packet)
    {
        session.Send(new InitCameraMessageComposer());
        return Task.CompletedTask;
    }
}

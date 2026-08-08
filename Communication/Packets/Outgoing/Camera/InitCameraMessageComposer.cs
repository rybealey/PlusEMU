using Plus.Communication.Packets;
using Plus.HabboHotel.GameClients;

namespace Plus.Communication.Packets.Outgoing.Camera;

public class InitCameraMessageComposer : IServerPacket
{
    public uint MessageId => ServerPacketHeader.InitCameraMessageComposer;

    public void Compose(IOutgoingPacket packet)
    {
        packet.WriteInteger(0); // credit price
        packet.WriteInteger(0); // ducket price
        packet.WriteInteger(0); // publish ducket price
    }
}

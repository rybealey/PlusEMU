using Plus.Communication.Packets;
using Plus.HabboHotel.GameClients;

namespace Plus.Communication.Packets.Outgoing.Camera;

public class CameraPurchaseOkComposer : IServerPacket
{
    public uint MessageId => ServerPacketHeader.CameraPurchaseOkComposer;

    public void Compose(IOutgoingPacket packet)
    {
        // no payload
    }
}

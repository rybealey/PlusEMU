using Plus.HabboHotel.GameClients;

namespace Plus.Communication.Packets.Outgoing.Users;

/// <summary>pixelrp: tells the client to open the Clothing Store window (:zara).</summary>
public class RpOpenClothingStoreComposer : IServerPacket
{
    public uint MessageId => ServerPacketHeader.RpOpenClothingStoreComposer;

    public void Compose(IOutgoingPacket packet)
    {
        packet.WriteInteger(1);
    }
}

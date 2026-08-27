using Plus.HabboHotel.GameClients;

namespace Plus.Communication.Packets.Outgoing.Users;

// pixelrp: result of a DiamondsStore purchase attempt.
// status: 0 = ok, 1 = not enough diamonds, 2 = backpack full.
public class DiamondsStorePurchaseResultComposer : IServerPacket
{
    private readonly int _status;
    private readonly string _itemKey;

    public uint MessageId => ServerPacketHeader.DiamondsStorePurchaseResultComposer;

    public DiamondsStorePurchaseResultComposer(int status, string itemKey)
    {
        _status = status;
        _itemKey = itemKey;
    }

    public void Compose(IOutgoingPacket packet)
    {
        packet.WriteInteger(_status);
        packet.WriteString(_itemKey);
    }
}

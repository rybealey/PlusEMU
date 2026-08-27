using Plus.HabboHotel.DiamondsStore;
using Plus.HabboHotel.GameClients;

namespace Plus.Communication.Packets.Outgoing.Users;

public class DiamondsStoreComposer : IServerPacket
{
    private readonly IReadOnlyList<DiamondsStoreItem> _items;

    public uint MessageId => ServerPacketHeader.DiamondsStoreComposer;

    public DiamondsStoreComposer(IReadOnlyList<DiamondsStoreItem> items)
    {
        _items = items;
    }

    public void Compose(IOutgoingPacket packet)
    {
        packet.WriteInteger(_items.Count);
        foreach (var item in _items)
        {
            packet.WriteString(item.ItemKey);
            packet.WriteString(item.Name);
            packet.WriteString(item.Description);
            packet.WriteString(item.Icon);
            packet.WriteInteger(item.Price);
            packet.WriteInteger(item.SpecialPrice ?? -1); // -1 = no sale
            packet.WriteInteger(item.VipDays);
        }
    }
}

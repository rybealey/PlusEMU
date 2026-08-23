using Plus.HabboHotel.GameClients;

namespace Plus.Communication.Packets.Outgoing.Users;

/// <summary>
/// pixelrp: the player's RP backpack contents (carry slots). Sent at login
/// and after every inventory change (:spawn, consuming an item).
/// </summary>
public class RpInventoryComposer : IServerPacket
{
    private readonly List<(int Slot, string Item, int Count)> _items;

    public uint MessageId => ServerPacketHeader.RpInventoryComposer;

    public RpInventoryComposer(List<(int Slot, string Item, int Count)> items)
    {
        _items = items ?? new();
    }

    public void Compose(IOutgoingPacket packet)
    {
        packet.WriteInteger(_items.Count);
        foreach (var (slot, item, count) in _items)
        {
            packet.WriteInteger(slot);
            packet.WriteString(item);
            packet.WriteInteger(count);
        }
    }
}

using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Users;

namespace Plus.Communication.Packets.Outgoing.Users;

/// <summary>
/// pixelrp: the player's RP backpack contents (carry slots). Sent at login
/// and after every inventory change (:spawn, consuming an item).
///
/// After the rows comes how worn each weapon and suit of armour is
/// (RpDurability): a count, then slot, wear and the wear it breaks at, for
/// the bar on the item. Read off the list RpInventoryStore.Load returns,
/// which is what every sender passes.
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

        var wear = (_items as RpInventoryList)?.Wear;
        var durable = _items.Where(entry => RpDurability.IsDurable(entry.Item)).ToList();
        packet.WriteInteger(durable.Count);
        foreach (var (slot, item, _) in durable)
        {
            packet.WriteInteger(slot);
            packet.WriteInteger((wear != null && wear.TryGetValue(slot, out var worn)) ? worn : 0);
            packet.WriteInteger(RpDurability.MaxOf(item));
        }
    }
}

using Plus.HabboHotel.Catalog.Clothing;
using Plus.HabboHotel.GameClients;

namespace Plus.Communication.Packets.Outgoing.Users;

/// <summary>
/// pixelrp: the Clothing Store's shelf - every priced catalog_clothing set.
/// The client works out the tab (head / torso / legs) from the part ids via
/// its figure data, and whether a piece is owned from the FigureSetIds list it
/// already holds. Re-sent after a purchase so LTD stock counts stay live.
/// </summary>
public class RpClothingStoreComposer : IServerPacket
{
    private readonly List<ClothingItem> _items;

    public uint MessageId => ServerPacketHeader.RpClothingStoreComposer;

    public RpClothingStoreComposer(IEnumerable<ClothingItem> items)
    {
        _items = items.Where(item => item.Price > 0).OrderBy(item => item.Id).ToList();
    }

    public void Compose(IOutgoingPacket packet)
    {
        packet.WriteInteger(_items.Count);
        foreach (var item in _items)
        {
            packet.WriteInteger(item.Id);
            packet.WriteString(item.ClothingName);
            packet.WriteString(item.DisplayName);
            packet.WriteString(item.Icon);
            packet.WriteInteger(item.Price);
            packet.WriteInteger(item.LtdTotal);
            packet.WriteInteger(item.LtdSold);
            packet.WriteInteger(item.PartIds.Count);
            foreach (var partId in item.PartIds)
                packet.WriteInteger(partId);
        }
    }
}

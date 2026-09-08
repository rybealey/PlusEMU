using Plus.HabboHotel.Items;

namespace Plus.Communication.Packets.Outgoing.Rooms.Furni;

/// <summary>
/// pixelrp: how see-through a placed item is, as a percentage.
///
/// One packet shape covers both uses - a count, then a pair per item - so a
/// single slider drag sends a count of one and room entry sends the whole set
/// in one go rather than a packet per item.
///
/// Only items that are NOT fully opaque travel: a room where nobody has
/// touched the tools sends a count of zero, which is the common case.
/// </summary>
public class RpFurniAlphaComposer : IServerPacket
{
    private readonly ICollection<Item> _items;

    public uint MessageId => ServerPacketHeader.RpFurniAlphaComposer;

    public RpFurniAlphaComposer(ICollection<Item> items)
    {
        _items = items;
    }

    public RpFurniAlphaComposer(Item item) : this(new List<Item> { item }) { }

    public void Compose(IOutgoingPacket packet)
    {
        packet.WriteInteger(_items.Count);
        foreach (var item in _items)
        {
            packet.WriteInteger((int)item.Id);
            packet.WriteInteger(item.Alpha);
        }
    }
}

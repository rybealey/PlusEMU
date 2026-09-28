using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Items;
using Plus.HabboHotel.Items.DataFormat;

namespace Plus.Communication.Packets.Outgoing.Rooms.Engine;

public static class RoomEngineSerializers
{
    public static void Serialize(this IOutgoingPacket packet, Item item)
    {
        packet.WriteUInteger(item.Id);
        packet.WriteInteger(item.Definition.SpriteId);
        packet.WriteInteger(item.GetX);
        packet.WriteInteger(item.GetY);
        packet.WriteInteger(item.Rotation);
        packet.WriteString(FormattableString.Invariant($"{item.GetZ}"));
        packet.WriteString(FormattableString.Invariant($"{item.Definition.Height}"));
        packet.WriteUInt(0);
        // pixelrp: group furni is sent with its group's (or its turf owner's)
        // badge and colours, built now - see ItemBehaviourUtility.GroupFurniData.
        ItemBehaviourUtility.Serialize(packet, (IFurniObjectData)ItemBehaviourUtility.GroupFurniData(item) ?? item.ExtraData, item.UniqueNumber, item.UniqueSeries);
        packet.WriteInteger(-1); // to-do: check
        packet.WriteInteger(item.Definition.Modes > 1 ? 1 : 0);
        packet.WriteInteger(item.UserId);
    }
    public static void Serialize(this IOutgoingPacket packet, ICollection<Item> items)
    {
        packet.WriteInt(items.Count);
        foreach (var item in items)
            packet.Serialize(item);
    }
}
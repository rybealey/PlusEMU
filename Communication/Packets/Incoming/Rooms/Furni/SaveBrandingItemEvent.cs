using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Items;

namespace Plus.Communication.Packets.Incoming.Rooms.Furni;

internal class SaveBrandingItemEvent : IPacketEvent
{
    public Task Parse(GameClient session, IIncomingPacket packet)
    {
        if (!session.GetHabbo().InRoom)
            return Task.CompletedTask;
        var room = session.GetHabbo().CurrentRoom;
        if (room == null)
            return Task.CompletedTask;
        if (!room.CheckRights(session, true) || !session.GetHabbo().Permissions.HasRight("room_item_save_branding_items"))
            return Task.CompletedTask;
        var itemId = packet.ReadUInt();
        var item = room.GetRoomItemHandler().GetItem(itemId);
        if (item == null)
            return Task.CompletedTask;
        if (item.Definition.InteractionType == InteractionType.Background)
        {
            // Nitro's SET_OBJECT_DATA: total string count, then flattened
            // key/value pairs. Must live in a MapDataFormat — the serializer
            // dispatches on the data object's type, and only MapDataFormat
            // reaches the client as the category-1 map its branding logic reads.
            var count = packet.ReadInt();
            var map = new Dictionary<string, string> { ["state"] = "0" };
            for (var i = 0; i < count / 2; i++)
            {
                var key = packet.ReadString();
                var value = packet.ReadString();
                if (!string.IsNullOrEmpty(key)) map[key] = value;
            }
            item.ExtraData = new Plus.HabboHotel.Items.DataFormat.MapDataFormat(map);
        }
        else if (item.Definition.InteractionType == InteractionType.FxProvider)
        {
            /*int Unknown = Packet.PopInt();
            string Data = Packet.PopString();
            int EffectId = Packet.PopInt();

            Item.ExtraData = Convert.ToString(EffectId);*/
        }
        room.GetRoomItemHandler().SetFloorItem(session, item, item.GetX, item.GetY, item.Rotation, false, false, true);
        return Task.CompletedTask;
    }
}
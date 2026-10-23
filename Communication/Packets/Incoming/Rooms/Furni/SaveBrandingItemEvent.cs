using Plus.Communication.Packets.Outgoing.Rooms.Engine;
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
        // pixelrp: owner rights, or on duty - see Room.CanEditBranding.
        if (!room.CanEditBranding(session))
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
            // pixelrp: saved and shown here, not through SetFloorItem. Only the
            // picture's settings changed, and SetFloorItem refuses a piece on a
            // square off the map - where a room ad stays once a floor plan
            // shrinks past it (LoadFurniture) - and, refused, marks nothing to
            // save: the picture would go back to its old place on the next load.
            room.GetRoomItemHandler().UpdateItem(item);
            room.SendPacket(new ObjectUpdateComposer(item));
            return Task.CompletedTask;
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
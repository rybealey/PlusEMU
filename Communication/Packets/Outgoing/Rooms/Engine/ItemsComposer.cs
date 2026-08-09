using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Items;
using Plus.HabboHotel.Rooms;

namespace Plus.Communication.Packets.Outgoing.Rooms.Engine;

public class ItemsComposer : IServerPacket
{
    private readonly Item[] _objects;
    private readonly Room _room;

    public uint MessageId => ServerPacketHeader.ItemsComposer;

    public ItemsComposer(Item[] objects, Room room)
    {
        _objects = objects;
        _room = room;
    }

    public void Compose(IOutgoingPacket packet)
    {
        // Same owner dictionary as ObjectsComposer: the client maps each wall item's
        // owner id to a name from here for the infostand.
        var owners = new Dictionary<int, string> { [_room.OwnerId] = _room.OwnerName };
        foreach (var item in _objects)
        {
            if (item.UserId > 0 && !owners.ContainsKey(item.UserId) && !string.IsNullOrEmpty(item.Username))
                owners[item.UserId] = item.Username;
        }
        packet.WriteInteger(owners.Count);
        foreach (var owner in owners)
        {
            packet.WriteInteger(owner.Key);
            packet.WriteString(owner.Value);
        }
        packet.WriteInteger(_objects.Length);
        // Wall items previously all claimed the room owner; write each item's real
        // owner (falling back to the room owner for legacy rows with no user_id).
        foreach (var item in _objects) WriteWallItem(packet, item, item.UserId > 0 ? item.UserId : _room.OwnerId);
    }

    private void WriteWallItem(IOutgoingPacket packet, Item item, int userId)
    {
        packet.WriteString(item.Id.ToString());
        packet.WriteInteger(item.Definition.SpriteId);
        try
        {
            packet.WriteString(item.WallCoordinates);
        }
        catch
        {
            packet.WriteString("");
        }
        ItemBehaviourUtility.GenerateWallExtradata(item, packet);
        packet.WriteInteger(-1);
        packet.WriteInteger(item.Definition.Modes > 1 ? 1 : 0);
        packet.WriteInteger(userId);
    }
}
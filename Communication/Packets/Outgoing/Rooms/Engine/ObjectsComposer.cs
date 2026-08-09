using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Items;
using Plus.HabboHotel.Rooms;

namespace Plus.Communication.Packets.Outgoing.Rooms.Engine;

public class ObjectsComposer : IServerPacket
{
    private readonly Item[] _objects;
    private readonly Room _room;
    public uint MessageId => ServerPacketHeader.ObjectsComposer;

    public ObjectsComposer(Item[] objects, Room room)
    {
        _objects = objects;
        _room = room;
    }

    public void Compose(IOutgoingPacket packet)
    {
        // The client resolves each object's owner name from this id->name dictionary;
        // writing only the room owner left every other player's furni unattributed in
        // the infostand.
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
        packet.Serialize(_objects);
    }
}

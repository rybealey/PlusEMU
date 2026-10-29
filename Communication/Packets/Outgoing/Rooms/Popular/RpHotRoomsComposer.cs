using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Rooms;

namespace Plus.Communication.Packets.Outgoing.Rooms.Popular;

/// <summary>
/// pixelrp: the :hotrooms window (HotRooms) - `open` set by the command, which
/// opens it, and clear on the updates that follow (a closed window ignores
/// those). Then the rooms, busiest first: id, name, players and the room's
/// most (for the navigator's green / yellow / red). The id is 0 unless the
/// player is staff.
/// </summary>
public class RpHotRoomsComposer : IServerPacket
{
    private readonly bool _open;
    private readonly List<HotRooms.Row> _rooms;
    private readonly bool _withIds;

    public uint MessageId => ServerPacketHeader.RpHotRoomsComposer;

    public RpHotRoomsComposer(bool open, List<HotRooms.Row> rooms, bool withIds)
    {
        _open = open;
        _rooms = rooms ?? new();
        _withIds = withIds;
    }

    public void Compose(IOutgoingPacket packet)
    {
        packet.WriteBoolean(_open);
        packet.WriteInteger(_rooms.Count);
        foreach (var room in _rooms)
        {
            packet.WriteInteger(_withIds ? (int)room.Id : 0);
            packet.WriteString(room.Name ?? string.Empty);
            packet.WriteInteger(room.Users);
            packet.WriteInteger(room.MaxUsers);
        }
    }
}

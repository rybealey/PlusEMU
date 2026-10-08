using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Rooms;

namespace Plus.Communication.Packets.Outgoing.Navigator;

/// <summary>
/// pixelrp: the zone of every room a navigator search just listed, sent right
/// after NavigatorSearchResultSetComposer - the navigator tags each row SAFE,
/// UNSAFE or TURF. A packet of its own because the room data in the results
/// is the stock shape every room packet shares, and cannot grow a field.
///
/// Count, then per room its id and zone: 0 unsafe, 1 safe, 2 turf (as
/// RpRoomZoneTypeSaveEvent numbers them). A room listed twice is sent once.
/// </summary>
public class RpNavigatorZonesComposer : IServerPacket
{
    private const int Unsafe = 0;
    private const int Safe = 1;
    private const int Turf = 2;

    private readonly List<RoomData> _rooms;

    public uint MessageId => ServerPacketHeader.RpNavigatorZonesComposer;

    public RpNavigatorZonesComposer(IEnumerable<RoomData> rooms)
    {
        _rooms = rooms.GroupBy(room => room.Id).Select(group => group.First()).ToList();
    }

    public void Compose(IOutgoingPacket packet)
    {
        packet.WriteInteger(_rooms.Count);
        foreach (var room in _rooms)
        {
            packet.WriteUInteger(room.Id);
            packet.WriteInteger(room.IsTurf ? Turf : room.IsSafeZone ? Safe : Unsafe);
        }
    }
}

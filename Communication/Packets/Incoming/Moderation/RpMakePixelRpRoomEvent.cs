using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Rooms;

namespace Plus.Communication.Packets.Incoming.Moderation;

/// <summary>
/// pixelrp: the Room Tool's "Make PixelRP room" - the room's id.
///
/// Staff only (the Room Tool's own `mod_tool`, and staff rank), and only for a
/// room that is loaded - the Room Tool can only show one that is. The work
/// itself is PixelRpRooms.MakePixelRpRoom.
/// </summary>
internal class RpMakePixelRpRoomEvent : IPacketEvent
{
    private readonly IRoomManager _roomManager;

    public RpMakePixelRpRoomEvent(IRoomManager roomManager)
    {
        _roomManager = roomManager;
    }

    public Task Parse(GameClient session, IIncomingPacket packet)
    {
        var habbo = session.GetHabbo();
        if (habbo == null || !habbo.Permissions.HasRight("mod_tool") || !PixelRpRooms.IsStaff(habbo))
            return Task.CompletedTask;
        var roomId = packet.ReadUInt();
        if (!_roomManager.TryGetRoom(roomId, out var room))
            return Task.CompletedTask;
        PixelRpRooms.MakePixelRpRoom(session, room);
        return Task.CompletedTask;
    }
}

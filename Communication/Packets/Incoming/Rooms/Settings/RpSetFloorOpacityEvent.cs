using Plus.Communication.Packets.Outgoing.Rooms.Settings;
using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Rooms;

namespace Plus.Communication.Packets.Incoming.Rooms.Settings;

/// <summary>
/// pixelrp: the Room tool's Floor opacity slider - a room's floor opacity, 0
/// to 100 (RoomFloorOpacity), sent when the slider is let go. Staff only
/// (Room.CanManageSettings), like the rest of the room's settings; any room,
/// not only the one the moderator stands in, as :topacity is.
/// </summary>
internal class RpSetFloorOpacityEvent : IPacketEvent
{
    private readonly IRoomManager _roomManager;

    public RpSetFloorOpacityEvent(IRoomManager roomManager)
    {
        _roomManager = roomManager;
    }

    public Task Parse(GameClient session, IIncomingPacket packet)
    {
        var roomId = packet.ReadUInt();
        var opacity = packet.ReadInt();
        // pixelrp (Twist, 2026-10-27): the rank first - it is the player's own
        // (Room.CanManageSettings reads nothing of the room), so a room is only
        // ever loaded for somebody allowed to change it.
        if (session.GetHabbo() == null || session.GetHabbo().Rank < Room.SettingsStaffRank)
            return Task.CompletedTask;
        if (!_roomManager.TryLoadRoom(roomId, out var room))
            return Task.CompletedTask;
        RoomFloorOpacity.Set(room, opacity);
        // The room was sent it; a moderator setting it from elsewhere gets
        // their own copy for the slider.
        if (session.GetHabbo().CurrentRoom != room)
            session.Send(new RpRoomFloorOpacityComposer(room.Id, room.FloorOpacity));
        return Task.CompletedTask;
    }
}

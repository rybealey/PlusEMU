using Plus.Communication.Packets.Outgoing.Rooms.Settings;
using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Rooms;

namespace Plus.Communication.Packets.Incoming.Rooms.Settings;

/// <summary>
/// Who has rights in a room - the Room tool's Rights tab. pixelrp: the packet
/// carries the room id, and the list is for THAT room (it used to be the
/// caller's current room whatever the packet said, so the Room tool showed an
/// empty list for any room the moderator was not standing in). Staff only,
/// like the rest of the room's settings (Room.CanManageSettings).
/// </summary>
internal class GetRoomRightsEvent : IPacketEvent
{
    private readonly IRoomManager _roomManager;

    public GetRoomRightsEvent(IRoomManager roomManager)
    {
        _roomManager = roomManager;
    }

    public Task Parse(GameClient session, IIncomingPacket packet)
    {
        var roomId = packet.ReadUInt();
        var instance = _roomManager.TryGetRoom(roomId, out var room) ? room : session.GetHabbo().CurrentRoom;
        if (instance == null)
            return Task.CompletedTask;
        if (!instance.CanManageSettings(session))
            return Task.CompletedTask;
        session.Send(new RoomRightsListComposer(instance));
        return Task.CompletedTask;
    }
}
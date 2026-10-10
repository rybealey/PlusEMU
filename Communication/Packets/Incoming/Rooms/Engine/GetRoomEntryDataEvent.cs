using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Quests;
using Plus.HabboHotel.Rooms;

namespace Plus.Communication.Packets.Incoming.Rooms.Engine;

internal class GetRoomEntryDataEvent : IPacketEvent
{
    private readonly IQuestManager _questManager;

    public GetRoomEntryDataEvent(IQuestManager questManager)
    {
        _questManager = questManager;
    }

    public Task Parse(GameClient session, IIncomingPacket packet)
    {
        var room = session.GetHabbo().CurrentRoom;
        if (room == null)
            return Task.CompletedTask;
        // pixelrp: the server has already sent this room, unasked, the moment
        // the room change was made (RoomEntry, from Habbo.EnterRoom). This is
        // the client asking for it as RoomReady reached it - answered, it would
        // all arrive a second time.
        if (session.GetHabbo().EntrySentRoomId == room.RoomId)
            return Task.CompletedTask;
        RoomEntry.Send(session, room, _questManager);
        return Task.CompletedTask;
    }
}

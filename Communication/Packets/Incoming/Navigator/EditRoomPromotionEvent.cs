using Plus.Communication.Attributes;
using Plus.Communication.Packets.Outgoing.Rooms.Engine;
using Plus.Database;
using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Rooms;
using Plus.HabboHotel.Rooms.Chat.Filter;

namespace Plus.Communication.Packets.Incoming.Navigator;

[StaffOnly]
internal class EditRoomPromotionEvent : IPacketEvent
{
    private readonly IWordFilterManager _wordFilterManager;
    private readonly IRoomManager _roomManager;
    private readonly IDatabase _database;

    public EditRoomPromotionEvent(IWordFilterManager wordFilterManager, IRoomManager roomManager, IDatabase database)
    {
        _wordFilterManager = wordFilterManager;
        _roomManager = roomManager;
        _database = database;
    }

    public Task Parse(GameClient session, IIncomingPacket packet)
    {
        // pixelrp: promoted rooms are gone (226_NoPromotedRooms) - nothing to edit
        return Task.CompletedTask;
    }
}
using Plus.Communication.Attributes;
using Plus.Communication.Packets.Outgoing.Navigator;
using Plus.Communication.Packets.Outgoing.Rooms.Notifications;
using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Navigator;
using Plus.HabboHotel.Rooms;
using Plus.HabboHotel.Rooms.Chat.Filter;

namespace Plus.Communication.Packets.Incoming.Navigator;

[StaffOnly]
internal class CreateFlatEvent : IPacketEvent
{
    private readonly IWordFilterManager _wordFilterManager;
    private readonly IRoomManager _roomManager;
    private readonly INavigatorManager _navigatorManager;

    public CreateFlatEvent(IWordFilterManager wordFilterManager, IRoomManager roomManager, INavigatorManager navigatorManager)
    {
        _wordFilterManager = wordFilterManager;
        _roomManager = roomManager;
        _navigatorManager = navigatorManager;
    }

    public Task Parse(GameClient session, IIncomingPacket packet)
    {
        var rooms = RoomFactory.GetRoomsDataByOwnerSortByName(session.GetHabbo().Id);
        if (rooms.Count >= 500)
        {
            session.Send(new CanCreateRoomComposer(true, 500));
            return Task.CompletedTask;
        }
        var name = _wordFilterManager.CheckMessage(packet.ReadString());
        var description = _wordFilterManager.CheckMessage(packet.ReadString());
        var modelName = packet.ReadString();
        var category = packet.ReadInt();
        var maxVisitors = packet.ReadInt(); //10 = min, 25 = max.
        var tradeSettings = packet.ReadInt(); //2 = All can trade, 1 = owner only, 0 = no trading.
        if (name.Length < 3 || name.Length > 60)
        {
            SendCreationError(session, "Room names must be between 3 and 60 characters.");
            return Task.CompletedTask;
        }
        if (!_roomManager.TryGetModel(modelName, out var model))
        {
            SendCreationError(session, "Your room could not be created: the selected room layout does not exist.");
            return Task.CompletedTask;
        }
        if (!_navigatorManager.TryGetSearchResultList(category, out var searchResultList) ||
            searchResultList.CategoryType != NavigatorCategoryType.Category ||
            searchResultList.RequiredRank > session.GetHabbo().Rank)
            category = 36;
        if (maxVisitors < 10 || maxVisitors > 25)
            maxVisitors = 10;
        if (tradeSettings < 0 || tradeSettings > 2)
            tradeSettings = 0;
        var newRoom = _roomManager.CreateRoom(session, name, description, category, maxVisitors, tradeSettings, model);
        if (newRoom != null)
            session.Send(new FlatCreatedComposer(newRoom.Id, name));
        else
            SendCreationError(session, "Something went wrong while creating your room. Please try again.");

        session.GetHabbo().Messenger.NotifyChangesToFriends();
        return Task.CompletedTask;
    }

    private static void SendCreationError(GameClient session, string message)
    {
        session.Send(new RoomNotificationComposer("room.creation_error", new Dictionary<string, string>
        {
            { "title", "Room Creation" },
            { "message", message }
        }));
    }
}
using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Navigator;
using Plus.HabboHotel.Rooms;

namespace Plus.Communication.Packets.Incoming.Rooms.Settings;

internal class SaveEnforcedCategorySettingsEvent : IPacketEvent
{
    private readonly IRoomManager _roomManager;
    private readonly INavigatorManager _navigationManager;

    public SaveEnforcedCategorySettingsEvent(IRoomManager roomManager, INavigatorManager navigatorManager)
    {
        _roomManager = roomManager;
        _navigationManager = navigatorManager;
    }

    public Task Parse(GameClient session, IIncomingPacket packet)
    {
        if (!_roomManager.TryGetRoom(packet.ReadUInt(), out var room))
            return Task.CompletedTask;
        if (!room.CheckRights(session, true))
            return Task.CompletedTask;
        var categoryId = packet.ReadInt();
        var tradeSettings = packet.ReadInt();
        if (tradeSettings < 0 || tradeSettings > 2)
            tradeSettings = 0;
        // Stock read searchResultList on the next line without checking it, so an unknown
        // category id - trivially injectable, since it comes straight off the packet - threw a
        // NullReferenceException out of the handler.
        if (!_navigationManager.TryGetSearchResultList(categoryId, out var searchResultList) || searchResultList == null ||
            searchResultList.CategoryType != NavigatorCategoryType.Category || searchResultList.RequiredRank > session.GetHabbo().Rank)
            categoryId = RoomCategories.FallbackId;
        // NOTE: stock never applies categoryId/tradeSettings to the room - this handler parses
        // and drops the packet. Left as-is; room settings are saved through SaveRoomSettingsEvent.
        return Task.CompletedTask;
    }
}
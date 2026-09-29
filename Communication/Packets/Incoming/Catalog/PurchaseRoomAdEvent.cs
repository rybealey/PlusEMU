using Plus.Communication.Attributes;
using Plus.Communication.Packets.Outgoing.Catalog;
using Plus.Communication.Packets.Outgoing.Rooms.Engine;
using Plus.Database;
using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Rooms;
using Plus.HabboHotel.Rooms.Chat.Filter;
using Plus.HabboHotel.Users.Messenger;
using Dapper;
using Plus.HabboHotel.Badges;
using Plus.HabboHotel.Friends;

namespace Plus.Communication.Packets.Incoming.Catalog;

[StaffOnly]
public class PurchaseRoomAdEvent : IPacketEvent
{
    private readonly IWordFilterManager _wordFilterManager;
    private readonly IDatabase _database;
    private readonly IBadgeManager _badgeManager;
    private readonly IMessengerDataLoader _messengerDataLoader;

    public PurchaseRoomAdEvent(IWordFilterManager wordFilterManager, IDatabase database, IBadgeManager badgeManager, IMessengerDataLoader messengerDataLoader)
    {
        _wordFilterManager = wordFilterManager;
        _database = database;
        _badgeManager = badgeManager;
        _messengerDataLoader = messengerDataLoader;
    }

    public async Task Parse(GameClient session, IIncomingPacket packet)
    {
        // pixelrp: promoted rooms are gone (226_NoPromotedRooms) - there is
        // nothing to buy, and the shop page that sold them is switched off.
        await Task.CompletedTask;
    }
}
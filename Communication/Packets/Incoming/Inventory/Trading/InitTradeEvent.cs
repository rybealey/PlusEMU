using Plus.HabboHotel.Rooms.Offers;
using Plus.Database;
using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Users.Accounts;
using Plus.Utilities;
using Dapper;

namespace Plus.Communication.Packets.Incoming.Inventory.Trading;

internal class InitTradeEvent : IPacketEvent
{
    private readonly IDatabase _database;

    public InitTradeEvent(IDatabase database)
    {
        _database = database;
    }

    public Task Parse(GameClient session, IIncomingPacket packet)
    {
        var userId = packet.ReadInt();
        if (!session.GetHabbo().InRoom)
            return Task.CompletedTask;
        var room = session.GetHabbo().CurrentRoom;
        if (room == null)
            return Task.CompletedTask;
        
        var roomUser = room.GetRoomUserManager().GetRoomUserByHabbo(session.GetHabbo().Id);
        if (roomUser == null)
            return Task.CompletedTask;
        if (Plus.HabboHotel.Rooms.KnockedOut.Refuse(session))
            return Task.CompletedTask;
        var targetUser = room.GetRoomUserManager().GetRoomUserByVirtualId(userId);
        // pixelrp :invisible: no trade with or by someone out of sight.
        if (targetUser == null || !roomUser.CanSee(targetUser) || !targetUser.CanSee(roomUser))
            return Task.CompletedTask;
        // pixelrp: one account's characters cannot trade with each other.
        // Two of your own people passing items back and forth is not a trade,
        // it is a wardrobe - and it would make the three-character limit a
        // way to triple an inventory rather than to play three people.
        if (AccountUtility.SameAccount(session.GetHabbo().Id, targetUser.UserId))
        {
            session.SendWhisper("That is one of your own characters.");
            return Task.CompletedTask;
        }
        if (session.GetHabbo().TradingLockExpiry > 0)
        {
            if (session.GetHabbo().TradingLockExpiry > UnixTimestamp.GetNow())
            {
                session.SendNotification("You're currently banned from trading.");
                return Task.CompletedTask;
            }
            session.GetHabbo().TradingLockExpiry = 0;
            session.SendNotification("Your trading ban has now expired.");
            using var connection = _database.Connection();
            connection.Execute("UPDATE `user_info` SET `trading_locked` = '0' WHERE `id` = @userId LIMIT 1", new { userId = session.GetHabbo().Id });
        }
        if (!session.GetHabbo().Permissions.HasRight("room_trade_override") &&
            (room.TradeSettings == 0 || (room.TradeSettings == 1 && room.OwnerId != session.GetHabbo().Id)))
        {
            session.SendWhisper("Trading is not allowed in this room.");
            return Task.CompletedTask;
        }
        var target = targetUser.GetClient();
        if (target?.GetHabbo() == null)
            return Task.CompletedTask;
        // pixelrp: Habbo's furni trade is gone. Trade sends a request on the
        // offer card, and a yes opens the backpack trade (RpTrades).
        var refusal = OfferState.StartTrade(room, roomUser, session, target);
        if (!string.IsNullOrEmpty(refusal))
            session.SendWhisper(refusal);
        return Task.CompletedTask;
    }
}
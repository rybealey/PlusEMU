using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using Dapper;
using Plus.Communication.Packets.Outgoing.Avatar;
using Plus.Communication.Packets.Outgoing.Inventory.Purse;
using Plus.Communication.Packets.Outgoing.Rooms.Apartments;
using Plus.Communication.Packets.Outgoing.Users.Banking;
using Plus.Core;
using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Items;
using Plus.HabboHotel.Users.Accounts;
using Plus.HabboHotel.Users.Banking;

namespace Plus.HabboHotel.Rooms;

/// <summary>
/// pixelrp: the real estate board (Twist, 2026-10-28; the "Apartments" canvas,
/// "Real Estate board"). Apartments their owners listed for sale on the
/// Apartments window's Sell tab (RpApartments, a $100 fee) - each with its
/// owner, its lots, its asking price, whether there is any furni in it, and a
/// picture of its inside (taken as its owner last walked out of it). Opened by
/// stepping onto a Real Estate point - SF Apartments' second action point,
/// item 24928 - and closed by stepping off, like the Apartments point.
///
/// Visit takes a player into a listed apartment as if through its door arrow,
/// so its doorbell does not stop them, and the arrow brings them back. Buy
/// moves the apartment WITH the furni of its seller inside to the buyer -
/// one apartment per player, never your own nor one of your own account's
/// other characters' - for its asking price in cash. The money goes to the
/// seller's checking account, online or not, as a Marketplace sale's does
/// (BankUtility.CreditMarketSale), or into their hand without one.
///
/// While it is listed nothing leaves it - no furni picked up, ejected, cashed
/// in or worn (RpApartments.IsListed) - so a buyer gets the furni a Visit
/// showed them (Twist, 2026-10-29).
///
/// `paid` (what selling back returns half of) is NOT the price a buyer paid:
/// it stays what the lots cost from the hotel, so buying an apartment dear
/// between friends and selling it back cannot make money from nothing.
///
/// Answered only for somebody on a Real Estate point; every change runs
/// under RpApartments' lock.
/// </summary>
public static class RpRealEstate
{
    public const int Refresh = 0;
    public const int Buy = 1;
    public const int Visit = 2;

    /// <summary>The most listings sent - the newest.</summary>
    private const int ListingLimit = 200;

    /// <summary>The least time between two changes (buy, visit) from one player.</summary>
    private const long ActionGapMs = 300;

    /// <summary>Whose board a Real Estate point opened. Only they are answered.</summary>
    private static readonly ConcurrentDictionary<int, byte> AtPoint = new();

    private static readonly ConcurrentDictionary<int, long> LastAction = new();

    private static NLog.ILogger Log => NLog.LogManager.GetLogger("Plus.HabboHotel.Rooms.RpRealEstate");

    public sealed class ListingRow
    {
        public uint RoomId { get; set; }
        public int OwnerId { get; set; }
        public string OwnerName { get; set; } = string.Empty;
        public string RoomName { get; set; } = string.Empty;
        public string Building { get; set; } = string.Empty;
        public string Lots { get; set; } = string.Empty;
        public int ListPrice { get; set; }
        public int ListedAt { get; set; }
        public uint LobbyRoomId { get; set; }
        public int FurniCount { get; set; }
        /// <summary>Theirs, or one of their own account's characters' - shown, never sold to them.</summary>
        public bool Mine { get; set; }
    }

    /// <summary>
    /// The owner's furni in the apartment, not its door arrow - what a buyer
    /// gets with it. Furni other players put there stays theirs, so it is not
    /// counted.
    /// </summary>
    private const string FurniCount =
        "(SELECT COUNT(*) FROM `items` i LEFT JOIN `rp_item_function` f ON f.`item_id` = i.`id` AND f.`field` = 'interaction_type' AND f.`value` = 'apartment_exit' " +
        "WHERE i.`room_id` = a.`room_id` AND i.`user_id` = a.`user_id` AND f.`item_id` IS NULL)";

    private const string ListingColumns =
        "a.`room_id` AS RoomId, a.`user_id` AS OwnerId, u.`username` AS OwnerName, COALESCE(r.`caption`, '') AS RoomName, " +
        "COALESCE(l.`caption`, '') AS Building, a.`lots` AS Lots, a.`list_price` AS ListPrice, a.`listed_at` AS ListedAt, " +
        "a.`lobby_room_id` AS LobbyRoomId, " + FurniCount + " AS FurniCount";

    private const string ListingFrom =
        "FROM `rp_apartments` a JOIN `users` u ON u.`id` = a.`user_id` LEFT JOIN `rooms` r ON r.`id` = a.`room_id` " +
        "LEFT JOIN `rooms` l ON l.`id` = a.`lobby_room_id` ";

    /// <summary>
    /// Not while its owner is banned from trading (user_info.trading_locked,
    /// :tradeban), as the Marketplace's SellerMayTrade: a listing put up before
    /// the ban is off the board, unsold and unvisited, until the ban ends.
    /// </summary>
    private const string OwnerMayTrade =
        "NOT EXISTS (SELECT 1 FROM `user_info` t WHERE t.`user_id` = a.`user_id` AND t.`trading_locked` > @now)";

    private static int Now => (int)DateTimeOffset.UtcNow.ToUnixTimeSeconds();

    /// <summary>Whether the player is standing on a Real Estate point.</summary>
    public static bool IsAtPoint(Room? room, RoomUser? user)
    {
        if (room == null || user == null)
            return false;
        var items = room.GetGameMap()?.GetAllRoomItemForSquare(user.X, user.Y);
        return items != null && items.Any(item => item?.Definition?.InteractionType == InteractionType.RealEstate);
    }

    /// <summary>The step onto a Real Estate point - on RoomTickWriter's thread, as RpApartments.OpenSoon.</summary>
    public static void OpenSoon(GameClient? client)
    {
        var habbo = client?.GetHabbo();
        if (habbo == null)
            return;
        RoomTickWriter.QueueOrdered(() =>
        {
            if (client!.GetHabbo() != habbo)
                return;
            var room = habbo.CurrentRoom;
            if (!IsAtPoint(room, room?.GetRoomUserManager()?.GetRoomUserByHabbo(habbo.Id)))
                return;
            AtPoint[habbo.Id] = 0;
            Send(client, true);
        });
    }

    /// <summary>Off a Real Estate point: the board comes down and stops being answered.</summary>
    public static void Close(GameClient? client)
    {
        var habbo = client?.GetHabbo();
        if (habbo == null)
            return;
        if (!AtPoint.TryRemove(habbo.Id, out _))
            return;
        client!.Send(new InClientLinkComposer("realestate/hide"));
    }

    /// <summary><see cref="Close"/> for the step off, behind the open it follows.</summary>
    public static void CloseSoon(GameClient? client)
    {
        if (client?.GetHabbo() == null)
            return;
        RoomTickWriter.QueueOrdered(() => Close(client));
    }

    /// <summary>Logout: the board is shut for them, and their timer goes.</summary>
    public static void Forget(int userId)
    {
        AtPoint.TryRemove(userId, out _);
        LastAction.TryRemove(userId, out _);
    }

    /// <summary>One action from the board (RpRealEstateActionEvent): buy or visit the apartment in this room, at the price they saw.</summary>
    public static void Handle(GameClient session, int action, int roomId, int price)
    {
        var habbo = session?.GetHabbo();
        if (habbo == null || action < Refresh || action > Visit)
            return;

        var room = habbo.CurrentRoom;
        if (!AtPoint.ContainsKey(habbo.Id) || !IsAtPoint(room, room?.GetRoomUserManager()?.GetRoomUserByHabbo(habbo.Id)))
        {
            Log.Warn("Real estate action {0} from {1} (id {2}) off a Real Estate point.", action, habbo.Username, habbo.Id);
            Close(session);
            return;
        }

        if (action == Refresh)
        {
            Send(session!, false);
            return;
        }
        var now = Environment.TickCount64;
        if (LastAction.TryGetValue(habbo.Id, out var acted) && now - acted < ActionGapMs)
        {
            Send(session!, false, "Slow down - try again in a moment.");
            return;
        }
        LastAction[habbo.Id] = now;

        string? notice;
        try
        {
            notice = action switch
            {
                Buy => BuyListing(session!, (uint)Math.Max(0, roomId), price),
                Visit => VisitListing(session!, room!, (uint)Math.Max(0, roomId)),
                _ => string.Empty
            };
        }
        catch (Exception e)
        {
            ExceptionLogger.LogException(e);
            notice = "Something went wrong - try again.";
        }
        // On their way to visit: the room change closed the board.
        if (notice != null)
            Send(session!, false, notice);
    }

    /// <summary>The board as this player sees it: every listing, newest first, and whether they own an apartment already.</summary>
    public static void Send(GameClient session, bool open, string notice = "")
    {
        var habbo = session?.GetHabbo();
        if (habbo == null)
            return;
        List<ListingRow> listings;
        using (var connection = PlusEnvironment.DatabaseManager.Connection())
        {
            listings = connection.Query<ListingRow>(
                $"SELECT {ListingColumns} {ListingFrom} WHERE a.`list_price` > 0 AND {OwnerMayTrade} ORDER BY a.`listed_at` DESC, a.`room_id` DESC LIMIT {ListingLimit}",
                new { now = Now }).ToList();
        }
        var root = AccountUtility.RootOf(habbo.Id);
        foreach (var listing in listings)
        {
            listing.Building = Building(listing.Building);
            listing.Mine = listing.OwnerId == habbo.Id || AccountUtility.RootOf(listing.OwnerId) == root;
        }
        var ownsOne = RpApartments.ApartmentOf(habbo.Id) != null;
        session!.Send(new RpRealEstateComposer(open, ownsOne, listings, notice));
    }

    /// <summary>"SF Apartments [Interior]" -> "SF Apartments".</summary>
    private static string Building(string roomName) =>
        Regex.Replace(roomName ?? string.Empty, @"\s*\[[^\]]*\]", string.Empty).Trim();

    private static ListingRow? ListingOf(uint roomId)
    {
        using var connection = PlusEnvironment.DatabaseManager.Connection();
        return connection.QueryFirstOrDefault<ListingRow>(
            $"SELECT {ListingColumns} {ListingFrom} WHERE a.`room_id` = @room AND a.`list_price` > 0 AND {OwnerMayTrade} LIMIT 1", new { room = roomId, now = Now });
    }

    /// <summary>
    /// Buy a listed apartment at the price they saw. The buyer's cash, the
    /// apartment row (still listed, still at that price, still the seller's),
    /// the room's owner and name, the seller's furni inside and the room's
    /// rights all change in ONE transaction. A room that is loaded is unloaded
    /// first - so its furni is saved and its owner read again - and everybody
    /// in it is sent to the room it was bought in; should the sale fail, they
    /// come straight back. The seller is paid after.
    /// </summary>
    private static string BuyListing(GameClient session, uint roomId, int price)
    {
        var habbo = session.GetHabbo();
        var refusal = RpApartments.TradeRefusal(session);
        if (refusal != null)
            return refusal;

        lock (RpApartments.Lock)
        {
            if (RpApartments.ApartmentOf(habbo.Id) != null)
                return "You already own an apartment.";
            var listing = ListingOf(roomId);
            if (listing == null)
                return "That apartment is no longer for sale.";
            if (listing.ListPrice != price)
                return "Its price has changed - have another look.";
            if (listing.OwnerId == habbo.Id || AccountUtility.SameAccount(listing.OwnerId, habbo.Id))
                return "You can't buy your own apartment.";
            if (habbo.Credits < price)
                return $"You need {RpApartments.Money(price - habbo.Credits)} more cash in hand.";

            var lots = RpApartments.LotsOf(new RpApartments.ApartmentRow { Lots = listing.Lots });
            var name = $"{habbo.Username}'s {(RpApartments.IsPenthouse(lots) ? "Penthouse" : "Apartment")}";
            if (name.Length > 60)
                name = name[..60];

            var roomManager = PlusEnvironment.Game.RoomManager;
            var inside = new List<RoomUser>();
            if (roomManager.TryGetRoom(roomId, out var loaded))
            {
                inside = loaded.GetRoomUserManager().GetRoomUsers().ToList();
                roomManager.UnloadRoom(roomId);
            }

            var sold = false;
            try
            {
                using var connection = PlusEnvironment.DatabaseManager.Connection();
                connection.Open();
                using var transaction = connection.BeginTransaction();
                if (!RpApartments.TakeCash(connection, transaction, habbo.Id, price))
                {
                    transaction.Rollback();
                    return $"You need {RpApartments.Money(price)} cash in hand.";
                }
                var moved = connection.Execute(
                    "UPDATE `rp_apartments` SET `user_id` = @buyer, `list_price` = 0, `listed_at` = 0, `bought_at` = UNIX_TIMESTAMP() " +
                    "WHERE `room_id` = @room AND `user_id` = @seller AND `list_price` = @price LIMIT 1",
                    new { buyer = habbo.Id, room = roomId, seller = listing.OwnerId, price }, transaction);
                if (moved != 1)
                {
                    transaction.Rollback();
                    return "That apartment is no longer for sale.";
                }
                connection.Execute("UPDATE `rooms` SET `owner` = @buyer, `caption` = @name WHERE `id` = @room LIMIT 1",
                    new { buyer = habbo.Id, name, room = roomId }, transaction);
                // The furni inside comes with it (Twist) - the seller's own; anybody
                // else's that was placed there stays theirs.
                connection.Execute("UPDATE `items` SET `user_id` = @buyer WHERE `room_id` = @room AND `user_id` = @seller",
                    new { buyer = habbo.Id, room = roomId, seller = listing.OwnerId }, transaction);
                connection.Execute("DELETE FROM `room_rights` WHERE `room_id` = @room", new { room = roomId }, transaction);
                connection.Execute("UPDATE `users` SET `home_room` = 0 WHERE `id` = @seller AND `home_room` = @room",
                    new { seller = listing.OwnerId, room = roomId }, transaction);
                transaction.Commit();
                sold = true;
            }
            finally
            {
                // Out to the building it stands in once sold; back in if not.
                var to = (sold && listing.LobbyRoomId != 0) ? listing.LobbyRoomId : roomId;
                foreach (var user in inside)
                {
                    var client = user?.GetClient();
                    if (client?.GetHabbo() == null)
                        continue;
                    client.GetHabbo().CurrentRoom = null;
                    client.SendRoomForward(to);
                }
            }

            habbo.ChangeCreditsInMemory(-price);
            CoinLedger.Record(habbo, -price, $"Real estate: bought room {roomId}");
            session.Send(new CreditBalanceComposer(habbo.Credits));
            PaySeller(listing, price);
            Log.Info("Real estate: {0} (id {1}) bought room {2} from {3} (id {4}) for {5}.",
                habbo.Username, habbo.Id, roomId, listing.OwnerName, listing.OwnerId, price);
            return $"You bought {listing.OwnerName}'s {(RpApartments.IsPenthouse(lots) ? "penthouse" : "apartment")}! Go to it from the Apartments point.";
        }
    }

    /// <summary>
    /// The sale's money to the seller: their checking account, online or not
    /// (BankUtility.CreditMarketSale), or their hand without one, so a sale
    /// never vanishes. Told if online.
    /// </summary>
    private static void PaySeller(ListingRow listing, int price)
    {
        var source = $"Real estate: sold room {listing.RoomId}";
        var paid = BankUtility.CreditMarketSale(listing.OwnerId, listing.OwnerName, price, source, out var account);
        var seller = PlusEnvironment.Game.ClientManager.GetClientByUserId(listing.OwnerId);
        var toBank = paid == BankResult.Ok;
        if (paid == BankResult.NoAccount)
        {
            seller?.GetHabbo()?.ChangeCreditsInMemory(price);
            using var connection = PlusEnvironment.DatabaseManager.Connection();
            connection.Execute("UPDATE `users` SET `credits` = `credits` + @amount WHERE `id` = @id LIMIT 1", new { amount = price, id = listing.OwnerId });
            var after = seller?.GetHabbo()?.Credits ?? connection.ExecuteScalar<int>("SELECT `credits` FROM `users` WHERE `id` = @id LIMIT 1", new { id = listing.OwnerId });
            CoinLedger.Record(listing.OwnerId, listing.OwnerName, price, after, source + " - no bank account");
        }
        else if (!toBank)
        {
            Log.Error("Real estate sale of room {0}: seller {1} was NOT paid {2} ({3}) - pay them by hand.", listing.RoomId, listing.OwnerId, price, paid);
            return;
        }
        if (seller?.GetHabbo() == null)
            return;
        seller.SendWhisper($"Your apartment sold on the real estate board for {RpApartments.Money(price)}, paid {(toBank ? "into your bank account" : "into your hand")}.");
        if (toBank && account != null)
            seller.Send(new RpBankAccountsComposer(account));
        else
            seller.Send(new CreditBalanceComposer(seller.GetHabbo().Credits));
        // Their Apartments window, should it be open, no longer shows it as theirs.
        RpApartments.Send(seller, false);
    }

    /// <summary>
    /// Into a listed apartment to look round, as if through its door arrow
    /// (RoomTransfers.StartArrow): a teleport, so its doorbell does not stop
    /// them, and they arrive on the arrow that brings them back. Null when
    /// they are on their way.
    /// </summary>
    private static string? VisitListing(GameClient session, Room from, uint roomId)
    {
        var habbo = session.GetHabbo();
        var refusal = RpApartments.Refusal(session);
        if (refusal != null)
            return refusal;
        if (ListingOf(roomId) == null)
            return "That apartment is no longer for sale.";
        uint arrow;
        using (var connection = PlusEnvironment.DatabaseManager.Connection())
            arrow = connection.ExecuteScalar<uint?>(
                "SELECT i.`id` FROM `items` i JOIN `rp_item_function` f ON f.`item_id` = i.`id` AND f.`field` = 'interaction_type' AND f.`value` = 'apartment_exit' " +
                "WHERE i.`room_id` = @room LIMIT 1", new { room = roomId }) ?? 0;
        if (arrow == 0)
            return "You can't visit that apartment right now.";
        if (!RoomTransfers.StartArrow(habbo, from, arrow))
            return "You are already on your way somewhere.";
        return null;
    }
}

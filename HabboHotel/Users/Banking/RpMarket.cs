using System.Collections.Concurrent;
using System.Data;
using Dapper;
using Plus.Communication.Packets.Outgoing.Avatar;
using Plus.Communication.Packets.Outgoing.Inventory.Purse;
using Plus.Communication.Packets.Outgoing.Users;
using Plus.Communication.Packets.Outgoing.Users.Banking;
using Plus.Core;
using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Items;
using Plus.HabboHotel.Rooms;
using Plus.HabboHotel.Rooms.Trading;
using Plus.HabboHotel.Users.Accounts;
using Plus.Utilities;

namespace Plus.HabboHotel.Users.Banking;

/// <summary>
/// pixelrp: the Marketplace (:mp; the "Marketplace" canvas, board A - Twist,
/// 2026-10-25). Players put backpack items up for sale at a price each, and
/// anybody else buys them one at a time with the cash in their hand. The money
/// goes straight into the seller's checking account, online or not. There is
/// no fee (Twist). Listings are anonymous: the window never names a seller,
/// nor a buyer to the seller.
///
/// A listed item LEAVES the backpack (RpInventoryStore.Discard) and lives in
/// rp_market_listings until it sells or its seller takes it back, so nothing
/// can be used, traded or stored while it is for sale. Weapons and armour keep
/// their wear (RpDurability) on the way in and out. Police gear never goes in
/// (RpTrades.Tradeable), and the trading gates hold for every action: not
/// knocked out, cuffed, jailed, banned from trading or in a trade.
///
/// Selling needs a bank account - that is where the money lands.
///
/// Every change runs under one lock. The market is small, and one lock makes
/// "two people buy the last one" impossible rather than unlikely.
///
/// The window's packet (RpMarketActionEvent) is answered only for somebody who
/// opened the market with :mp this session - so everything the command checks
/// (its permission, being in a room) holds for every action too - or who is on
/// a Marketplace point (furni with the `marketplace` behaviour: step on and it
/// opens, step off and it closes and stops answering again), and changes
/// are spaced at least <see cref="ActionGapMs"/> apart, refreshes
/// <see cref="RefreshGapMs"/>. Nothing the packet carries is trusted beyond a
/// slot, a count, a price and a listing id, each checked here.
/// </summary>
public static class RpMarket
{
    public const int Refresh = 0;
    public const int ListItem = 1;
    public const int BuyItem = 2;
    public const int TakeBack = 3;

    /// <summary>How many listings one player may have up at once - the same for everybody, VIP or not (Twist, 2026-10-25).</summary>
    public const int MaxListings = 100;

    /// <summary>The most a listing may ask for one item (Twist, 2026-10-25).</summary>
    public const int MaxPrice = 200_000;

    /// <summary>The newest listings sent to Browse; the window filters and sorts them itself.</summary>
    private const int BrowseLimit = 500;

    /// <summary>The most rows My listings is sent - every listing still up comes first.</summary>
    private const int MineLimit = 200;

    /// <summary>How long a listing that sold stays on My listings after it ended.</summary>
    private const int SoldShownSeconds = 72 * 3600;

    private static readonly object Lock = new();

    /// <summary>How long the market stays open after :mp or the last action - an hour of idle, then :mp again.</summary>
    private const long SessionMs = 60 * 60 * 1000;

    /// <summary>The least time between two changes (list, buy, take back) from one player.</summary>
    private const long ActionGapMs = 300;

    /// <summary>The least time between two refreshes from one player; each one reads the market twice.</summary>
    private const long RefreshGapMs = 1000;

    /// <summary>Who opened the market with :mp, and when they last used it.</summary>
    private static readonly ConcurrentDictionary<int, long> Opened = new();

    private static readonly ConcurrentDictionary<int, long> LastAction = new();

    private static readonly ConcurrentDictionary<int, long> LastRefresh = new();

    private static NLog.ILogger Log => NLog.LogManager.GetLogger("Plus.HabboHotel.Users.Banking.RpMarket");

    private static int Now => (int)DateTimeOffset.UtcNow.ToUnixTimeSeconds();

    public sealed class MarketRow
    {
        public int Id { get; set; }
        public int SellerId { get; set; }
        public string SellerName { get; set; } = string.Empty;
        public string Item { get; set; } = string.Empty;
        public int Wear { get; set; }
        public int Price { get; set; }
        public int ListedQty { get; set; }
        public int LeftQty { get; set; }
        public int SoldQty { get; set; }
        public int ListedAt { get; set; }
        public int EndedAt { get; set; }
    }

    private const string Columns =
        "`id` AS Id, `seller_id` AS SellerId, `seller_name` AS SellerName, `item` AS Item, `wear` AS Wear, " +
        "`price` AS Price, `listed_qty` AS ListedQty, `left_qty` AS LeftQty, `sold_qty` AS SoldQty, " +
        "`listed_at` AS ListedAt, `ended_at` AS EndedAt";

    /// <summary>:mp - opens the window, or says why not.</summary>
    public static void Open(GameClient session)
    {
        var refusal = Refusal(session);
        if (refusal != null)
        {
            session.SendWhisper(refusal);
            return;
        }
        Opened[session.GetHabbo().Id] = Environment.TickCount64;
        Send(session, true);
    }

    /// <summary>
    /// Whether the player is standing on a Marketplace point - furni with the
    /// `marketplace` behaviour, like the Armoury's action point (item 11562).
    /// </summary>
    public static bool IsAtMarket(Room? room, RoomUser? user)
    {
        if (room == null || user == null)
            return false;
        var items = room.GetGameMap()?.GetAllRoomItemForSquare(user.X, user.Y);
        return items != null && items.Any(item => item?.Definition?.InteractionType == InteractionType.Marketplace);
    }

    /// <summary>
    /// <see cref="Open"/> for the step onto a Marketplace point (Twist,
    /// 2026-10-26). The step fires on the movement thread and Open reads the
    /// database, so - as DepositBox.OpenSoon - it runs on RoomTickWriter's
    /// thread, in order with <see cref="CloseSoon"/>, and only for somebody
    /// still standing on one by then.
    /// </summary>
    public static void OpenSoon(GameClient? client)
    {
        var habbo = client?.GetHabbo();
        if (habbo == null)
            return;
        RoomTickWriter.QueueOrdered(() =>
        {
            // Same session as the step - a relog in between is a new one.
            if (client!.GetHabbo() != habbo)
                return;
            var room = habbo.CurrentRoom;
            if (!IsAtMarket(room, room?.GetRoomUserManager()?.GetRoomUserByHabbo(habbo.Id)))
                return;
            Open(client);
        });
    }

    /// <summary>
    /// Off a Marketplace point: the window comes down and the market is shut
    /// for them until they step back on or type :mp. A close needs no
    /// database, so the click to walk sends it at once (OwnWalk).
    /// </summary>
    public static void Close(GameClient? client)
    {
        var habbo = client?.GetHabbo();
        if (habbo == null)
            return;
        Opened.TryRemove(habbo.Id, out _);
        client!.Send(new InClientLinkComposer("marketplace/hide"));
    }

    /// <summary>
    /// <see cref="Close"/> for the step off. Through the same queue as
    /// <see cref="OpenSoon"/>, so it can never overtake the open it follows.
    /// </summary>
    public static void CloseSoon(GameClient? client)
    {
        if (client?.GetHabbo() == null)
            return;
        RoomTickWriter.QueueOrdered(() => Close(client));
    }

    /// <summary>Logout: the market is shut for them, and their timers go.</summary>
    public static void Forget(int userId)
    {
        Opened.TryRemove(userId, out _);
        LastAction.TryRemove(userId, out _);
        LastRefresh.TryRemove(userId, out _);
    }

    /// <summary>One action from the window (RpMarketActionEvent). Answers with the whole market again.</summary>
    public static void Handle(GameClient session, int action, int a, int b, int c)
    {
        if (session == null)
            return;
        var habbo = session.GetHabbo();
        if (habbo == null || action < Refresh || action > TakeBack)
            return;

        // Only for somebody who opened the market with :mp; the window never
        // sends without it, so anything else did not come from the window.
        var now = Environment.TickCount64;
        if (!Opened.TryGetValue(habbo.Id, out var opened))
        {
            Log.Warn("Marketplace action {0} from {1} (id {2}) without the market open.", action, habbo.Username, habbo.Id);
            return;
        }
        if (now - opened > SessionMs)
        {
            Opened.TryRemove(habbo.Id, out _);
            Send(session, false, "The marketplace closed after an hour - type :mp to open it again.");
            return;
        }
        Opened[habbo.Id] = now;

        if (action == Refresh)
        {
            if (LastRefresh.TryGetValue(habbo.Id, out var refreshed) && now - refreshed < RefreshGapMs)
                return;
            LastRefresh[habbo.Id] = now;
            Send(session, false);
            return;
        }
        // Too soon after the last change: answered, so the window's buttons
        // come back, but nothing is done.
        if (LastAction.TryGetValue(habbo.Id, out var acted) && now - acted < ActionGapMs)
        {
            Send(session, false);
            return;
        }
        LastAction[habbo.Id] = now;

        string notice;
        try
        {
            notice = action switch
            {
                ListItem => List(session, a, b, c),
                BuyItem => Buy(session, a),
                TakeBack => Take(session, a),
                _ => string.Empty
            };
        }
        catch (Exception e)
        {
            // Each step puts back what it took when a later one fails; this
            // only stops a surprise from leaving the window without an answer.
            ExceptionLogger.LogException(e);
            notice = "Something went wrong - try again.";
        }
        Send(session, false, notice);
    }

    /// <summary>
    /// The market as this player sees it: everybody else's listings, their
    /// own, whether they can sell, and what to say about their last action.
    /// `open` brings the window up; without it a closed window ignores this.
    /// </summary>
    public static void Send(GameClient session, bool open, string notice = "")
    {
        var habbo = session?.GetHabbo();
        if (habbo == null)
            return;
        try
        {
            using var connection = PlusEnvironment.DatabaseManager.Connection();
            // Not your own, nor your other characters' - they cannot be bought
            // from here (Buy).
            var browse = connection.Query<MarketRow>(
                $"SELECT {Columns} FROM `rp_market_listings` WHERE `ended_at` = 0 AND `left_qty` > 0 AND `seller_id` <> @me " +
                "AND `seller_id` NOT IN (SELECT `id` FROM `users` WHERE `id` = @root OR `parent_id` = @root) " +
                $"ORDER BY `id` DESC LIMIT {BrowseLimit}", new { me = habbo.Id, root = AccountUtility.RootOf(habbo.Id) }).ToList();
            var mine = connection.Query<MarketRow>(
                $"SELECT {Columns} FROM `rp_market_listings` WHERE `seller_id` = @me " +
                "AND (`ended_at` = 0 OR (`sold_qty` > 0 AND `ended_at` >= @since)) " +
                $"ORDER BY (`ended_at` = 0) DESC, `id` DESC LIMIT {MineLimit}", new { me = habbo.Id, since = Now - SoldShownSeconds }).ToList();
            session!.Send(new RpMarketComposer(open, BankUtility.HasAccount(habbo.Id), browse, mine, notice, Now));
        }
        catch (Exception e)
        {
            ExceptionLogger.LogException(e);
        }
    }

    /// <summary>Why this player cannot use the market right now, or null. The trading gates.</summary>
    private static string? Refusal(GameClient session)
    {
        var habbo = session.GetHabbo();
        if (RpTrades.IsTrading(habbo.Id))
            return RpTrades.FrozenMessage;
        return RpTrades.StateRefusal(session);
    }

    /// <summary>
    /// Put `quantity` of what sits in one carry slot up for sale at `price` each.
    ///
    /// The items leave the backpack in ONE guarded statement (TakeFromSlot):
    /// only if that slot still holds that item, at least that many and - for a
    /// weapon or armour - the same wear. A trade, the deposit box or using one
    /// in between, and nothing is taken. If the listing then cannot be written,
    /// the items go straight back.
    /// </summary>
    private static string List(GameClient session, int slot, int quantity, int price)
    {
        var habbo = session.GetHabbo();
        var refusal = Refusal(session);
        if (refusal != null)
            return refusal;
        if (!BankUtility.HasAccount(habbo.Id))
            return "Open a bank account at Mercury Bank to sell on the marketplace.";
        // Only the carry slots: an equipped weapon is put away first.
        if (slot < 1 || slot > Habbo.RpCarrySlots)
            return string.Empty;
        if (price < 1)
            return "Set a price first.";
        if (price > MaxPrice)
            return $"The most you can ask is {TextHandling.GetMoney(MaxPrice)}.";

        lock (Lock)
        {
            var entry = habbo.LoadRpInventory().FirstOrDefault(row => row.Slot == slot);
            if (string.IsNullOrEmpty(entry.Item) || entry.Count < 1)
                return string.Empty;
            if (!RpTrades.Tradeable(entry.Item))
                return "Police equipment cannot be sold.";

            using var connection = PlusEnvironment.DatabaseManager.Connection();
            var live = connection.ExecuteScalar<int>(
                "SELECT COUNT(*) FROM `rp_market_listings` WHERE `seller_id` = @me AND `ended_at` = 0", new { me = habbo.Id });
            if (live >= MaxListings)
                return $"You can only have {MaxListings} listings up at once.";

            var count = Math.Clamp(quantity, 1, entry.Count);
            var wear = RpDurability.IsDurable(entry.Item) ? RpInventoryStore.WearOf(habbo.Id, slot) : 0;
            if (!TakeFromSlot(connection, habbo.Id, slot, entry.Item, count, wear))
                return "Your backpack changed - try again.";

            try
            {
                connection.Execute(
                    "INSERT INTO `rp_market_listings` (`seller_id`, `seller_name`, `item`, `wear`, `price`, `listed_qty`, `left_qty`, `sold_qty`, `listed_at`, `ended_at`) " +
                    "VALUES (@me, @name, @item, @wear, @price, @count, @count, 0, @now, 0)",
                    new { me = habbo.Id, name = habbo.Username, item = entry.Item, wear, price, count, now = Now });
            }
            catch (Exception e)
            {
                ExceptionLogger.LogException(e);
                GiveBack(habbo, entry.Item, count, wear, "a listing that could not be saved");
                session.Send(new RpInventoryComposer(habbo.LoadRpInventory()));
                return "That could not be listed - your items are back in your backpack.";
            }
            session.Send(new RpInventoryComposer(habbo.LoadRpInventory()));
            var each = count > 1 ? " each" : string.Empty;
            return $"{Capital(Named(entry.Item, count))} is now for sale at {TextHandling.GetMoney(price)}{each}.";
        }
    }

    /// <summary>
    /// Buy one of a listing with the cash in hand. Its money goes into the
    /// seller's checking account.
    ///
    /// The ONE item is claimed first, in a guarded statement that only lands on
    /// the listing exactly as it was read - so nothing reaches the backpack
    /// until it is this buyer's, and two buyers can never get the same one. If
    /// the backpack then cannot take it, the claim is undone and it goes back
    /// on sale. Only once it is in the backpack is the cash taken and the
    /// seller paid.
    /// </summary>
    private static string Buy(GameClient session, int listingId)
    {
        var habbo = session.GetHabbo();
        var refusal = Refusal(session);
        if (refusal != null)
            return refusal;

        MarketRow? row;
        BankAccount? sellerAccount = null;
        var paidBank = false;
        lock (Lock)
        {
            using var connection = PlusEnvironment.DatabaseManager.Connection();
            row = connection.QueryFirstOrDefault<MarketRow>(
                $"SELECT {Columns} FROM `rp_market_listings` WHERE `id` = @id AND `ended_at` = 0 AND `left_qty` > 0 LIMIT 1",
                new { id = listingId });
            if (row == null)
                return "That is no longer for sale.";
            // pixelrp: nor one of your own account's characters'. Bought by a
            // sibling, a listing moved a backpack's items and a hand's cash
            // between one person's characters, which a trade and Pixel Cash
            // both refuse - and with the price paid into the seller's checking
            // it passed Pixel Cash's daily cap and harvested each new
            // character's starting cash.
            if (row.SellerId == habbo.Id || AccountUtility.SameAccount(row.SellerId, habbo.Id))
                return "That is your own listing.";
            if (!RpTrades.Tradeable(row.Item) || row.Price < 1)
                return "That cannot be bought.";
            if (habbo.Credits < row.Price)
                return $"You need {TextHandling.GetMoney(row.Price)} in cash to buy that.";

            var left = row.LeftQty - 1;
            var claimed = connection.Execute(
                "UPDATE `rp_market_listings` SET `left_qty` = @left, `sold_qty` = `sold_qty` + 1, `ended_at` = @ended " +
                "WHERE `id` = @id AND `ended_at` = 0 AND `left_qty` = @was LIMIT 1",
                new { left, ended = left <= 0 ? Now : 0, id = row.Id, was = row.LeftQty });
            if (claimed != 1)
                return "That is no longer for sale.";

            int slot;
            try
            {
                slot = RpInventoryStore.Add(habbo.Id, row.Item, habbo.RpUnlockedSlots, row.Wear);
            }
            catch (Exception e)
            {
                ExceptionLogger.LogException(e);
                slot = -100;
            }
            if (slot < 0)
            {
                Unclaim(connection, row);
                if (slot == Habbo.RpAlreadyHeld)
                    return "You can only carry one of those.";
                return slot == -1 ? "Your backpack is full." : "Something went wrong - nothing was bought.";
            }

            // The users row takes the same relative change, as the ATM's does
            // (Habbo.ChangeCredits, BankUtility.PersistHandCredits).
            habbo.ChangeCreditsInMemory(-row.Price);
            Try(() => connection.Execute("UPDATE `users` SET `credits` = `credits` - @price WHERE `id` = @me LIMIT 1",
                new { price = row.Price, me = habbo.Id }));
            Try(() => connection.Execute(
                "INSERT INTO `rp_market_sales` (`listing_id`, `seller_id`, `buyer_id`, `buyer_name`, `item`, `wear`, `price`, `sold_at`) " +
                "VALUES (@listing, @seller, @buyer, @buyerName, @item, @wear, @price, @now)",
                new { listing = row.Id, seller = row.SellerId, buyer = habbo.Id, buyerName = habbo.Username, item = row.Item, wear = row.Wear, price = row.Price, now = Now }));

            var paid = BankUtility.CreditMarketSale(row.SellerId, row.SellerName, row.Price,
                $"Marketplace: sold {RpTrades.ItemName(row.Item)}", out sellerAccount);
            paidBank = paid == BankResult.Ok;
            // The seller has no account any more: the money goes into their
            // hand instead, so a sale never vanishes.
            if (paid == BankResult.NoAccount)
                Try(() => PayIntoHand(row.SellerId, row.Price));
            else if (!paidBank)
                Log.Error("Marketplace sale of listing {0}: seller {1} was NOT paid {2} ({3}) - pay them by hand.",
                    row.Id, row.SellerId, row.Price, paid);
        }

        session.Send(new CreditBalanceComposer(habbo.Credits));
        session.Send(new RpInventoryComposer(habbo.LoadRpInventory()));

        var seller = PlusEnvironment.Game.ClientManager.GetClientByUserId(row.SellerId);
        if (seller?.GetHabbo() != null)
        {
            var where = paidBank ? "into your bank account" : "into your hand";
            seller.SendWhisper($"Your {RpTrades.ItemName(row.Item)} sold on the marketplace for {TextHandling.GetMoney(row.Price)}, paid {where}.");
            if (paidBank && sellerAccount != null)
                seller.Send(new RpBankAccountsComposer(sellerAccount));
            else
                seller.Send(new CreditBalanceComposer(seller.GetHabbo().Credits));
            Send(seller, false);
        }
        return $"You bought {Named(row.Item, 1)} for {TextHandling.GetMoney(row.Price)}.";
    }

    /// <summary>
    /// Take what is left of one of your listings back into the backpack.
    ///
    /// The listing is closed FIRST, in a guarded statement, so nothing reaches
    /// the backpack while it is still on sale - it cannot be bought and taken
    /// back at once. What the backpack cannot hold goes back on sale.
    /// </summary>
    private static string Take(GameClient session, int listingId)
    {
        var habbo = session.GetHabbo();
        var refusal = Refusal(session);
        if (refusal != null)
            return refusal;

        lock (Lock)
        {
            using var connection = PlusEnvironment.DatabaseManager.Connection();
            var row = connection.QueryFirstOrDefault<MarketRow>(
                $"SELECT {Columns} FROM `rp_market_listings` WHERE `id` = @id AND `seller_id` = @me AND `ended_at` = 0 AND `left_qty` > 0 LIMIT 1",
                new { id = listingId, me = habbo.Id });
            if (row == null)
                return string.Empty;

            var claimed = connection.Execute(
                "UPDATE `rp_market_listings` SET `left_qty` = 0, `ended_at` = @now " +
                "WHERE `id` = @id AND `seller_id` = @me AND `ended_at` = 0 AND `left_qty` = @was LIMIT 1",
                new { now = Now, id = row.Id, me = habbo.Id, was = row.LeftQty });
            if (claimed != 1)
                return string.Empty;

            var returned = 0;
            var result = 0;
            try
            {
                while (returned < row.LeftQty)
                {
                    result = RpInventoryStore.Add(habbo.Id, row.Item, habbo.RpUnlockedSlots, row.Wear);
                    if (result < 0)
                        break;
                    returned++;
                }
            }
            catch (Exception e)
            {
                ExceptionLogger.LogException(e);
            }

            var left = row.LeftQty - returned;
            if (left > 0)
                Try(() => connection.Execute("UPDATE `rp_market_listings` SET `left_qty` = @left, `ended_at` = 0 WHERE `id` = @id LIMIT 1",
                    new { left, id = row.Id }));
            else if (row.SoldQty == 0)
                Try(() => connection.Execute("DELETE FROM `rp_market_listings` WHERE `id` = @id LIMIT 1", new { id = row.Id }));
            session.Send(new RpInventoryComposer(habbo.LoadRpInventory()));

            if (returned == 0)
                return result == Habbo.RpAlreadyHeld ? "You can only carry one of those." : "Your backpack is full.";
            return left > 0
                ? $"Took back {returned} - your backpack is full."
                : $"{Capital(Named(row.Item, returned))} is back in your backpack.";
        }
    }

    /// <summary>
    /// Takes `count` of `item` out of one backpack slot in a single guarded
    /// statement - only if the slot still holds that item, enough of it and,
    /// for a weapon or armour, the same wear. True when it was taken.
    /// </summary>
    private static bool TakeFromSlot(IDbConnection connection, int userId, int slot, string item, int count, int wear)
    {
        var sameWear = RpDurability.IsDurable(item) ? " AND `wear` = @wear" : string.Empty;
        var args = new { userId, slot, item, count, wear };
        var taken = connection.Execute(
                "UPDATE `user_rp_inventory` SET `count` = `count` - @count " +
                $"WHERE `user_id` = @userId AND `slot` = @slot AND `item` = @item AND `count` > @count{sameWear} LIMIT 1", args) == 1
            || connection.Execute(
                "DELETE FROM `user_rp_inventory` " +
                $"WHERE `user_id` = @userId AND `slot` = @slot AND `item` = @item AND `count` = @count{sameWear} LIMIT 1", args) == 1;
        // The last equipped vape gone empties the hand, as RpInventoryStore.Discard does.
        if (taken && item == RpVape.Item)
            RpVape.SyncHand(userId);
        return taken;
    }

    /// <summary>Items back into a backpack after something failed; anything that will not fit is logged for staff.</summary>
    private static void GiveBack(Habbo habbo, string item, int count, int wear, string why)
    {
        var given = 0;
        try
        {
            while (given < count && RpInventoryStore.Add(habbo.Id, item, habbo.RpUnlockedSlots, wear) >= 0)
                given++;
        }
        catch (Exception e)
        {
            ExceptionLogger.LogException(e);
        }
        if (given < count)
            Log.Error("Marketplace: {0} of {1} could not go back into {2}'s backpack after {3} - give them back by hand.",
                count - given, item, habbo.Username, why);
    }

    /// <summary>A claimed item goes back on sale, its listing as it was.</summary>
    private static void Unclaim(IDbConnection connection, MarketRow row) =>
        Try(() => connection.Execute(
            "UPDATE `rp_market_listings` SET `left_qty` = `left_qty` + 1, `sold_qty` = `sold_qty` - 1, `ended_at` = 0 WHERE `id` = @id LIMIT 1",
            new { id = row.Id }));

    /// <summary>A write that must not stop the rest: logged, not thrown.</summary>
    private static void Try(Action write)
    {
        try
        {
            write();
        }
        catch (Exception e)
        {
            ExceptionLogger.LogException(e);
        }
    }

    /// <summary>A sale's money into the seller's hand, when there is no account to put it in.</summary>
    private static void PayIntoHand(int userId, int amount)
    {
        var client = PlusEnvironment.Game.ClientManager.GetClientByUserId(userId);
        var habbo = client?.GetHabbo();
        if (habbo != null)
            habbo.ChangeCreditsInMemory(amount);
        using var connection = PlusEnvironment.DatabaseManager.Connection();
        connection.Execute("UPDATE `users` SET `credits` = `credits` + @amount WHERE `id` = @userId LIMIT 1", new { amount, userId });
    }

    /// <summary>"Medkit ×3", or "a Knife" for one.</summary>
    private static string Named(string item, int count)
    {
        var name = RpTrades.ItemName(item);
        if (count > 1)
            return $"{name} ×{count}";
        return ("AEIOU".IndexOf(char.ToUpperInvariant(name.FirstOrDefault())) >= 0 ? "an " : "a ") + name;
    }

    private static string Capital(string text) =>
        string.IsNullOrEmpty(text) ? text : char.ToUpperInvariant(text[0]) + text[1..];
}

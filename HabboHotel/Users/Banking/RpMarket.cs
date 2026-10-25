using Dapper;
using Plus.Communication.Packets.Outgoing.Inventory.Purse;
using Plus.Communication.Packets.Outgoing.Users;
using Plus.Communication.Packets.Outgoing.Users.Banking;
using Plus.Core;
using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Rooms.Trading;
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
        Send(session, true);
    }

    /// <summary>One action from the window (RpMarketActionEvent). Answers with the whole market again.</summary>
    public static void Handle(GameClient session, int action, int a, int b, int c)
    {
        if (session?.GetHabbo() == null)
            return;
        var notice = action switch
        {
            ListItem => List(session, a, b, c),
            BuyItem => Buy(session, a),
            TakeBack => Take(session, a),
            _ => string.Empty
        };
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
            var browse = connection.Query<MarketRow>(
                $"SELECT {Columns} FROM `rp_market_listings` WHERE `ended_at` = 0 AND `left_qty` > 0 AND `seller_id` <> @me " +
                $"ORDER BY `id` DESC LIMIT {BrowseLimit}", new { me = habbo.Id }).ToList();
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

    /// <summary>Put `quantity` of what sits in one carry slot up for sale at `price` each.</summary>
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
            if (string.IsNullOrEmpty(entry.Item))
                return string.Empty;
            if (!RpTrades.Tradeable(entry.Item))
                return "Police equipment cannot be sold.";

            using var connection = PlusEnvironment.DatabaseManager.Connection();
            var live = connection.ExecuteScalar<int>(
                "SELECT COUNT(*) FROM `rp_market_listings` WHERE `seller_id` = @me AND `ended_at` = 0", new { me = habbo.Id });
            if (live >= MaxListings)
                return $"You can only have {MaxListings} listings up at once.";

            var wear = RpDurability.IsDurable(entry.Item) ? RpInventoryStore.WearOf(habbo.Id, slot) : 0;
            var (item, removed) = RpInventoryStore.Discard(habbo.Id, slot, Math.Clamp(quantity, 1, entry.Count));
            if (removed <= 0 || item != entry.Item)
                return string.Empty;

            connection.Execute(
                "INSERT INTO `rp_market_listings` (`seller_id`, `seller_name`, `item`, `wear`, `price`, `listed_qty`, `left_qty`, `sold_qty`, `listed_at`, `ended_at`) " +
                "VALUES (@me, @name, @item, @wear, @price, @count, @count, 0, @now, 0)",
                new { me = habbo.Id, name = habbo.Username, item, wear, price, count = removed, now = Now });
            session.Send(new RpInventoryComposer(habbo.LoadRpInventory()));
            var each = removed > 1 ? " each" : string.Empty;
            return $"{Capital(Named(item, removed))} is now for sale at {TextHandling.GetMoney(price)}{each}.";
        }
    }

    /// <summary>Buy one of a listing with the cash in hand. Its money goes into the seller's checking account.</summary>
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
            if (row.SellerId == habbo.Id)
                return "That is your own listing.";
            if (habbo.Credits < row.Price)
                return $"You need {TextHandling.GetMoney(row.Price)} in cash to buy that.";

            var slot = RpInventoryStore.Add(habbo.Id, row.Item, habbo.RpUnlockedSlots, row.Wear);
            if (slot == Habbo.RpAlreadyHeld)
                return "You can only carry one of those.";
            if (slot < 0)
                return "Your backpack is full.";

            // Habbo.Credits is the authority; the users row is the crash hedge,
            // the relative update the ATM makes (BankUtility.PersistHandCredits).
            habbo.Credits -= row.Price;
            connection.Execute("UPDATE `users` SET `credits` = `credits` - @price WHERE `id` = @me LIMIT 1",
                new { price = row.Price, me = habbo.Id });

            var left = row.LeftQty - 1;
            connection.Execute(
                "UPDATE `rp_market_listings` SET `left_qty` = @left, `sold_qty` = `sold_qty` + 1, `ended_at` = @ended WHERE `id` = @id LIMIT 1",
                new { left, ended = left <= 0 ? Now : 0, id = row.Id });
            connection.Execute(
                "INSERT INTO `rp_market_sales` (`listing_id`, `seller_id`, `buyer_id`, `buyer_name`, `item`, `wear`, `price`, `sold_at`) " +
                "VALUES (@listing, @seller, @buyer, @buyerName, @item, @wear, @price, @now)",
                new { listing = row.Id, seller = row.SellerId, buyer = habbo.Id, buyerName = habbo.Username, item = row.Item, wear = row.Wear, price = row.Price, now = Now });

            var paid = BankUtility.CreditMarketSale(row.SellerId, row.SellerName, row.Price,
                $"Marketplace: sold {RpTrades.ItemName(row.Item)}", out sellerAccount);
            paidBank = paid == BankResult.Ok;
            // The seller has no account any more: the money goes into their
            // hand instead, so a sale never vanishes.
            if (paid == BankResult.NoAccount)
                PayIntoHand(row.SellerId, row.Price);
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

    /// <summary>Take what is left of one of your listings back into the backpack.</summary>
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
                $"SELECT {Columns} FROM `rp_market_listings` WHERE `id` = @id AND `seller_id` = @me AND `ended_at` = 0 LIMIT 1",
                new { id = listingId, me = habbo.Id });
            if (row == null)
                return string.Empty;

            var returned = 0;
            var result = 0;
            while (returned < row.LeftQty)
            {
                result = RpInventoryStore.Add(habbo.Id, row.Item, habbo.RpUnlockedSlots, row.Wear);
                if (result < 0)
                    break;
                returned++;
            }
            if (returned == 0)
                return result == Habbo.RpAlreadyHeld ? "You can only carry one of those." : "Your backpack is full.";

            var left = row.LeftQty - returned;
            if (left <= 0 && row.SoldQty == 0)
                connection.Execute("DELETE FROM `rp_market_listings` WHERE `id` = @id LIMIT 1", new { id = row.Id });
            else
                connection.Execute("UPDATE `rp_market_listings` SET `left_qty` = @left, `ended_at` = @ended WHERE `id` = @id LIMIT 1",
                    new { left, ended = left <= 0 ? Now : 0, id = row.Id });
            session.Send(new RpInventoryComposer(habbo.LoadRpInventory()));
            return left > 0
                ? $"Took back {returned} - your backpack is full."
                : $"{Capital(Named(row.Item, returned))} is back in your backpack.";
        }
    }

    /// <summary>A sale's money into the seller's hand, when there is no account to put it in.</summary>
    private static void PayIntoHand(int userId, int amount)
    {
        var client = PlusEnvironment.Game.ClientManager.GetClientByUserId(userId);
        var habbo = client?.GetHabbo();
        if (habbo != null)
            habbo.Credits += amount;
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

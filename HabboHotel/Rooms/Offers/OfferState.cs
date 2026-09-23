using System.Collections.Concurrent;
using Plus.Communication.Packets.Outgoing.Rooms.Engine;
using Plus.Communication.Packets.Outgoing.Users;
using Plus.Communication.Packets.Outgoing.Users.Banking;
using Plus.HabboHotel.Corporations;
using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Users;
using Plus.HabboHotel.Users.Accounts;
using Plus.HabboHotel.Users.Banking;
using Plus.Utilities;

namespace Plus.HabboHotel.Rooms.Offers;

/// <summary>
/// pixelrp: :offer / :sell - one player selling something to another, answered
/// with a card above the buyer's chat bar.
///
/// THE GOODS ARE MINTED, NOT HANDED OVER. A medkit sold here is created in the
/// buyer's backpack; the seller never had one. That is what makes this a SHOP
/// and not a trade, and it is the whole reason the command is gated the way it
/// is: on-duty hospital staff, holding the right tool. Without both halves it
/// is a button that prints free medkits for anybody who types it.
///
/// The tool is the second half and it is not decoration. A medic sells
/// painkillers out of the packet in their hand and gives a shot with the
/// syringe in their hand; walking to the counter to pick the thing up is the
/// roleplay, and the check is what makes it one.
///
/// NOTHING MOVES UNTIL THE BUYER TAPS. An offer is a promise, not an escrow -
/// no money is held, no item is reserved. Everything is therefore checked
/// TWICE: once to word the card, and once at the tap, because thirty seconds
/// is long enough for any of it to stop being true.
/// </summary>
public static class OfferState
{
    /// <summary>How long a card stands before it goes away by itself.</summary>
    public const int LifetimeSeconds = 30;

    /// <summary>Cards one buyer may have waiting. The oldest is the one shown.</summary>
    public const int MaxQueued = 3;

    /// <summary>The most of anything one offer may carry.</summary>
    public const int MaxQuantity = 10;

    /// <summary>What can be sold, and what it takes to sell it.</summary>
    public sealed class Ware
    {
        public string Key = string.Empty;
        /// <summary>Shown on the card. Plural is chosen by quantity.</summary>
        public string One = string.Empty;
        public string Many = string.Empty;
        /// <summary>False for anything that is an ACT rather than a thing - a heal is one shot or none.</summary>
        public bool TakesQuantity;
        /// <summary>Handitem the seller must be holding. 0 = nothing required.</summary>
        public int RequiredHandItem;
        public string RequiredHandItemName = string.Empty;
        /// <summary>Dollars. Zero while the hospital is being tested; the payment path still runs.</summary>
        public int Price;
        /// <summary>True when accepting puts something in the backpack, which needs room for it.</summary>
        public bool GoesInBackpack;
    }

    // Handitem ids from the hotel's own ExternalTexts: 1013 Painkiller, 1014
    // Syringe. Named here rather than inline so the next ward's worth of items
    // is a row in this table and nothing else.
    public const int PainkillerHandItem = 1013;
    public const int SyringeHandItem = 1014;

    public static readonly Dictionary<string, Ware> Catalogue = new(StringComparer.OrdinalIgnoreCase)
    {
        ["medkit"] = new Ware
        {
            Key = "medkit", One = "Medkit", Many = "Medkits", TakesQuantity = true,
            RequiredHandItem = PainkillerHandItem, RequiredHandItemName = "painkillers",
            Price = 0, GoesInBackpack = true
        },
        ["heal"] = new Ware
        {
            Key = "heal", One = "Heal", Many = "Heals", TakesQuantity = false,
            RequiredHandItem = SyringeHandItem, RequiredHandItemName = "a syringe",
            Price = 0, GoesInBackpack = false
        }
    };

    /// <summary>A heal puts back this much of the bar at once; the rest fills in.</summary>
    public const int HealPercent = 70;

    public sealed class Offer
    {
        public int Id;
        public int SellerId;
        public string SellerName = string.Empty;
        public int BuyerId;
        public string BuyerName = string.Empty;
        public Ware Ware = null!;
        public int Quantity;
        public int Price;
        public uint RoomId;
        public DateTime ExpiresAt;

        public int Total => Price * Quantity;
        public string Label => (Quantity == 1) ? Ware.One : Ware.Many;
    }

    private static int _nextId;
    private static readonly ConcurrentDictionary<int, Offer> Live = new();

    // ---- reading -------------------------------------------------------------

    /// <summary>Every live offer for this buyer, oldest first.</summary>
    private static List<Offer> ForBuyer(int buyerId) =>
        Live.Values.Where(offer => offer.BuyerId == buyerId).OrderBy(offer => offer.Id).ToList();

    /// <summary>The one on screen, and how many are behind it.</summary>
    public static Offer? Showing(int buyerId, out int queued)
    {
        var all = ForBuyer(buyerId);
        queued = all.Count;
        return all.FirstOrDefault();
    }

    public static bool SellerHasOpen(int sellerId) => Live.Values.Any(offer => offer.SellerId == sellerId);

    // ---- can this happen at all ---------------------------------------------

    /// <summary>
    /// Why the buyer cannot take this, in their own words, or null when they
    /// can. Asked when the card is drawn AND again when it is tapped: the two
    /// answers are allowed to differ, and the second one is the one that
    /// decides.
    /// </summary>
    public static string? BlockedFor(Offer offer, Habbo buyer)
    {
        if (buyer == null)
            return "They are not here any more.";
        if (offer.Ware.GoesInBackpack && !CanTake(buyer, offer.Ware.Key, offer.Quantity))
            return "Your backpack is full";
        if (offer.Total > 0 && !CanAfford(buyer, offer.Total))
            return $"You cannot afford {TextHandling.GetMoney(offer.Total)}";
        return null;
    }

    /// <summary>
    /// Whether this many of an item would actually fit, worked out the same way
    /// AddRpItem places them: onto a part-used stack of the same thing first,
    /// then into a free slot. Asked before anything is added, because half a
    /// purchase is worse than none.
    /// </summary>
    public static bool CanTake(Habbo buyer, string item, int quantity)
    {
        if (buyer == null || quantity <= 0)
            return false;
        var inventory = buyer.LoadRpInventory();
        var room = new Dictionary<int, int>();
        var used = new HashSet<int>();
        foreach (var entry in inventory)
        {
            used.Add(entry.Slot);
            if (entry.Item == item && entry.Slot >= 1 && entry.Slot <= buyer.RpUnlockedSlots)
                room[entry.Slot] = Math.Max(0, Habbo.RpStackCap - entry.Count);
        }
        var space = room.Values.Sum();
        for (var slot = 1; slot <= buyer.RpUnlockedSlots; slot++)
            if (!used.Contains(slot))
                space += Habbo.RpStackCap;
        return space >= quantity;
    }

    /// <summary>Checking first, then what is in hand - the same order the charge uses.</summary>
    public static bool CanAfford(Habbo buyer, long amount)
    {
        if (buyer == null || amount <= 0)
            return true;
        var account = BankUtility.Get(buyer.Id) ?? BankUtility.EnsureLoaded(buyer.Id);
        return ((account?.Current ?? 0) >= amount) || (buyer.Credits >= amount);
    }

    // ---- making one ----------------------------------------------------------

    public enum StartResult { Ok, UnknownItem, NotStaff, NoTool, BadQuantity, BuyerBusy, SellerBusy, Self, SameAccount }

    /// <summary>
    /// Put an offer on the buyer's screen. Every refusal is the SELLER's
    /// problem - the buyer is never told an offer was attempted and refused,
    /// because from their side it did not happen.
    /// </summary>
    public static StartResult Start(Room room, RoomUser sellerUser, Habbo seller, Habbo buyer, string itemKey,
        int quantity, out Offer? offer)
    {
        offer = null;
        if (room == null || sellerUser == null || seller == null || buyer == null)
            return StartResult.UnknownItem;
        if (seller.Id == buyer.Id)
            return StartResult.Self;
        if (AccountUtility.SameAccount(seller.Id, buyer.Id))
            return StartResult.SameAccount;
        if (!Catalogue.TryGetValue(itemKey ?? string.Empty, out var ware))
            return StartResult.UnknownItem;
        if (!MedicalUtility.IsOnDutyHospitalStaff(seller.Id))
            return StartResult.NotStaff;
        // The tool in hand. Checked against the ROOM user, because a handitem
        // is a thing about an avatar standing somewhere and not about an
        // account.
        if (ware.RequiredHandItem != 0 && sellerUser.CarryItemId != ware.RequiredHandItem)
            return StartResult.NoTool;

        if (!ware.TakesQuantity)
            quantity = 1;
        if (quantity < 1 || quantity > MaxQuantity)
            return StartResult.BadQuantity;
        if (SellerHasOpen(seller.Id))
            return StartResult.SellerBusy;
        Showing(buyer.Id, out var queued);
        if (queued >= MaxQueued)
            return StartResult.BuyerBusy;

        offer = new Offer
        {
            Id = Interlocked.Increment(ref _nextId),
            SellerId = seller.Id,
            SellerName = seller.Username,
            BuyerId = buyer.Id,
            BuyerName = buyer.Username,
            Ware = ware,
            Quantity = quantity,
            Price = ware.Price,
            RoomId = room.RoomId,
            ExpiresAt = DateTime.UtcNow.AddSeconds(LifetimeSeconds)
        };
        Live[offer.Id] = offer;
        Push(buyer.Id);
        return StartResult.Ok;
    }

    // ---- answering -----------------------------------------------------------

    public static void Decline(int offerId, Habbo buyer)
    {
        if (buyer == null || !Live.TryGetValue(offerId, out var offer) || offer.BuyerId != buyer.Id)
            return;
        Live.TryRemove(offerId, out _);
        Push(buyer.Id);
        PlusEnvironment.Game.ClientManager.GetClientByUserId(offer.SellerId)?
            .SendWhisper($"{buyer.Username} turned down your offer.");
    }

    /// <summary>The seller taking it back, from :offer cancel or from leaving.</summary>
    public static void CancelBySeller(int sellerId)
    {
        foreach (var offer in Live.Values.Where(entry => entry.SellerId == sellerId).ToList())
        {
            Live.TryRemove(offer.Id, out _);
            Push(offer.BuyerId);
        }
    }

    /// <summary>Everything either side of a player, for a room leave or a disconnect.</summary>
    public static void Forget(int userId)
    {
        foreach (var offer in Live.Values.Where(entry => entry.SellerId == userId || entry.BuyerId == userId).ToList())
        {
            Live.TryRemove(offer.Id, out _);
            if (offer.BuyerId != userId)
                Push(offer.BuyerId);
        }
    }

    public static bool Accept(int offerId, GameClient buyerSession)
    {
        var buyer = buyerSession?.GetHabbo();
        if (buyer == null || !Live.TryGetValue(offerId, out var offer) || offer.BuyerId != buyer.Id)
            return false;

        var sellerSession = PlusEnvironment.Game.ClientManager.GetClientByUserId(offer.SellerId);
        var seller = sellerSession?.GetHabbo();
        var room = buyer.CurrentRoom;

        // EVERYTHING AGAIN. The card was worded thirty seconds ago.
        if (seller == null || room == null || room.RoomId != offer.RoomId || seller.CurrentRoom?.RoomId != offer.RoomId)
        {
            Live.TryRemove(offerId, out _);
            Push(buyer.Id);
            buyerSession!.SendWhisper($"{offer.SellerName} is no longer here.");
            return false;
        }
        var sellerUser = room.GetRoomUserManager()?.GetRoomUserByHabbo(seller.Id);
        if (!MedicalUtility.IsOnDutyHospitalStaff(seller.Id) ||
            (offer.Ware.RequiredHandItem != 0 && sellerUser?.CarryItemId != offer.Ware.RequiredHandItem))
        {
            Live.TryRemove(offerId, out _);
            Push(buyer.Id);
            buyerSession!.SendWhisper($"{offer.SellerName} cannot complete that sale any more.");
            sellerSession?.SendWhisper($"Your offer to {buyer.Username} lapsed - you are no longer able to make it.");
            return false;
        }
        var blocked = BlockedFor(offer, buyer);
        if (blocked != null)
        {
            Push(buyer.Id);
            buyerSession!.SendWhisper(blocked + ".");
            return false;
        }

        // Money first: an item handed over before the payment fails is a gift.
        if (offer.Total > 0 && !Charge(buyer, seller, offer))
        {
            Push(buyer.Id);
            buyerSession!.SendWhisper("That payment could not be completed.");
            return false;
        }

        Live.TryRemove(offerId, out _);
        Deliver(room, offer, buyerSession!, buyer);
        Push(buyer.Id);

        // THE TOOL IS SPENT, and only on a sale that happened. A packet of
        // painkillers handed over is a packet gone; a syringe used is used.
        // A refusal or a lapse leaves it in their hand to run its own carry
        // timer out, because nothing was administered - the medic is still
        // standing there holding it, which is exactly what they should look
        // like while they try the next person.
        if (offer.Ware.RequiredHandItem != 0 && sellerUser != null &&
            sellerUser.CarryItemId == offer.Ware.RequiredHandItem)
            sellerUser.CarryItem(0);

        var sum = (offer.Total > 0) ? $" for {TextHandling.GetMoney(offer.Total)}" : string.Empty;
        sellerSession?.SendWhisper($"{buyer.Username} accepted {offer.Quantity} {offer.Label}{sum}.");
        return true;
    }

    // ---- the money -----------------------------------------------------------

    /// <summary>
    /// Checking first, the wallet second, exactly as asked. The two are not
    /// mixed: a purchase is paid from ONE of them, because a charge that takes
    /// half from each leaves a player unable to say what they paid with.
    /// </summary>
    private static bool Charge(Habbo buyer, Habbo seller, Offer offer)
    {
        var amount = offer.Total;
        var note = $"{offer.Quantity} {offer.Label}";
        var paid = BankUtility.TryDebitChecking(buyer.Id, buyer.Username, amount, $"Bought {note}", out var buyerAccount);
        if (paid)
        {
            buyer.Client?.Send(new RpBankAccountsComposer(buyerAccount));
        }
        else
        {
            if (buyer.Credits < amount)
                return false;
            buyer.Credits -= (int)amount;
            buyer.Client?.Send(new Communication.Packets.Outgoing.Inventory.Purse.CreditBalanceComposer(buyer.Credits));
        }

        // The seller is paid into checking when they have one, and in cash when
        // they do not. Never refused: the buyer has already paid, and money
        // that cannot be banked still has a pocket to go in.
        if (BankUtility.CreditChecking(seller.Id, seller.Username, amount, $"Sold {note} to {buyer.Username}", out var sellerAccount))
            seller.Client?.Send(new RpBankAccountsComposer(sellerAccount));
        else
        {
            seller.Credits += (int)amount;
            seller.Client?.Send(new Communication.Packets.Outgoing.Inventory.Purse.CreditBalanceComposer(seller.Credits));
        }
        return true;
    }

    // ---- the goods -----------------------------------------------------------

    private static void Deliver(Room room, Offer offer, GameClient buyerSession, Habbo buyer)
    {
        if (offer.Ware.Key == "heal")
        {
            HealUp(room, buyerSession, buyer);
            return;
        }

        for (var i = 0; i < offer.Quantity; i++)
            buyer.AddRpItem(offer.Ware.Key);
        buyerSession.Send(new RpInventoryComposer(buyer.LoadRpInventory()));
    }

    /// <summary>
    /// Most of the bar at once, and the rest of it over the next minute.
    ///
    /// The jab is what you paid for and it lands immediately; the drip after it
    /// is the medicine working, which is RpRegen's whole job and is already how
    /// a snack and a medical bed behave. Capped, so a heal bought at 80 health
    /// simply fills the bar and has nothing left to run.
    /// </summary>
    private static void HealUp(Room room, GameClient session, Habbo buyer)
    {
        var jab = Math.Max(1, (buyer.RpHealthMax * HealPercent) / 100);
        buyer.RpHealth = Math.Min(buyer.RpHealthMax, buyer.RpHealth + jab);
        if (buyer.RpHealth < buyer.RpHealthMax)
            buyer.RpHealthRegen.Start(buyer.RpHealthMax);
        else
            buyer.RpHealthRegen.Stop();

        var user = room?.GetRoomUserManager()?.GetRoomUserByHabbo(buyer.Id);
        if (user != null)
        {
            user.UpdateRpKnockoutState();
            room!.SendPacket(new RpStatsComposer(user.VirtualId, buyer.RpHealth, buyer.RpHealthMax,
                buyer.RpEnergy, buyer.RpEnergyMax, (int)Math.Round(buyer.RpAggression),
                buyer.IsRpPassive ? 1 : 0, buyer.Rank >= 5 ? 1 : 0));
        }
        session.SendWhisper("You feel much better.");
    }

    // ---- the clock -----------------------------------------------------------

    /// <summary>
    /// Drop what has run out. Memory only - no database, no packets except to
    /// the two people concerned - so it is safe on the room cycle.
    /// </summary>
    public static void Tick()
    {
        if (Live.IsEmpty)
            return;
        var now = DateTime.UtcNow;
        foreach (var offer in Live.Values.Where(entry => entry.ExpiresAt <= now).ToList())
        {
            if (!Live.TryRemove(offer.Id, out _))
                continue;
            Push(offer.BuyerId);
            PlusEnvironment.Game.ClientManager.GetClientByUserId(offer.SellerId)?
                .SendWhisper($"Your offer to {offer.BuyerName} expired.");
        }
    }

    // ---- telling the client --------------------------------------------------

    /// <summary>
    /// Send the buyer whatever is at the front of their queue, or the empty
    /// card that takes it off screen. One packet either way: the client never
    /// has to work out whether something was removed.
    /// </summary>
    public static void Push(int buyerId)
    {
        var session = PlusEnvironment.Game.ClientManager.GetClientByUserId(buyerId);
        if (session?.GetHabbo() == null)
            return;
        var offer = Showing(buyerId, out var queued);
        var blocked = (offer == null) ? null : BlockedFor(offer, session.GetHabbo());
        session.Send(new RpOfferComposer(offer, queued, blocked));
    }
}

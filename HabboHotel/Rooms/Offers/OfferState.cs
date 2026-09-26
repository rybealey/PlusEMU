using System.Collections.Concurrent;
using Plus.Communication.Packets.Outgoing.Inventory.Purse;
using Plus.Communication.Packets.Outgoing.Rooms.Engine;
using Plus.Communication.Packets.Outgoing.Users;
using Plus.HabboHotel.Corporations;
using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Users;
using Plus.HabboHotel.Users.Accounts;
using Plus.HabboHotel.Users.Relationships;
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
/// MONEY IS CASH IN HAND, NEVER THE BANK. A sale takes what the buyer is
/// carrying and pays it straight into what the seller is carrying. That is the
/// whole reason the hotel's ATMs exist: a balance you have to go and fetch is a
/// trip and a machine to stand at, and a shop that quietly charged the card
/// would make every one of those machines decorative.
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

    /// <summary>
    /// What a proposer holds while their proposal stands - raised the moment
    /// they ask, lowered the moment it is answered, lapses or is withdrawn.
    /// Carried and never raised to the mouth: the client lists it with the
    /// phone in AvatarLogic's CARRY_ONLY_HAND_IDS.
    /// </summary>
    public const int ProposalHandItem = 299;

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

    /// <summary>
    /// What a card is asking. A SALE is goods for money under every rule above;
    /// a PROPOSAL is :propose, which borrows the card and the queue and nothing
    /// else - no catalogue, no staff gate, no tool, no money, no backpack.
    /// </summary>
    public enum OfferKind { Sale, Proposal }

    public sealed class Offer
    {
        public int Id;
        public OfferKind Kind = OfferKind.Sale;
        public int SellerId;
        public string SellerName = string.Empty;
        public int BuyerId;
        public string BuyerName = string.Empty;
        /// <summary>Null for a proposal, which sells nothing.</summary>
        public Ware? Ware;
        public int Quantity;
        public int Price;
        public uint RoomId;
        public DateTime ExpiresAt;

        public bool IsProposal => Kind == OfferKind.Proposal;
        public int Total => Price * Quantity;
        public string Key => IsProposal ? "proposal" : Ware!.Key;
        public string Label => IsProposal ? "proposal" : (Quantity == 1) ? Ware!.One : Ware!.Many;
        /// <summary>The noun the seller's whispers use: "your offer", "your proposal".</summary>
        public string Noun => IsProposal ? "proposal" : "offer";
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
        // A proposal costs nothing and puts nothing in a backpack. Whether
        // either of them has married somebody else since is asked at the tap,
        // where the answer can still change what happens.
        if (offer.IsProposal)
            return null;
        if (offer.Ware!.GoesInBackpack && !CanTake(buyer, offer.Ware.Key, offer.Quantity))
            return "Your backpack is full";
        if (offer.Total > 0 && !CanAfford(buyer, offer.Total))
            return $"You are not carrying {TextHandling.GetMoney(offer.Total)}";
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

    /// <summary>
    /// CASH IN HAND, and nothing else. A sale never reaches for the bank.
    ///
    /// Deliberate, and the whole reason the ATMs exist: money in an account is
    /// money you have to go and fetch, which is a trip across the hotel and a
    /// machine to stand at. A shop that quietly charged the card would make
    /// every one of those machines decorative.
    /// </summary>
    public static bool CanAfford(Habbo buyer, long amount) =>
        (buyer != null) && ((amount <= 0) || (buyer.Credits >= amount));

    // ---- making one ----------------------------------------------------------

    public enum StartResult
    {
        Ok, UnknownItem, NotStaff, NoTool, BadQuantity, BuyerBusy, SellerBusy, Self, SameAccount,
        TooFar, SellerPartnered, BuyerPartnered
    }

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

    /// <summary>
    /// :propose - put a proposal on the other player's card. Same queue, same
    /// thirty seconds and the same one-open-at-a-time rule as a sale, because a
    /// proposal IS an offer; what it does not share is every rule about goods.
    ///
    /// Reach is the eight tiles around the proposer (Chebyshev 1, diagonals in),
    /// the same as :hug and :push - you propose to someone standing with you.
    /// The partnership checks come after the cheap ones because they are the two
    /// database reads here.
    /// </summary>
    public static StartResult StartProposal(Room room, RoomUser sellerUser, Habbo seller, Habbo buyer, out Offer? offer)
    {
        offer = null;
        if (room == null || sellerUser == null || seller == null || buyer == null)
            return StartResult.UnknownItem;
        if (seller.Id == buyer.Id)
            return StartResult.Self;
        if (AccountUtility.SameAccount(seller.Id, buyer.Id))
            return StartResult.SameAccount;
        var buyerUser = room.GetRoomUserManager()?.GetRoomUserByHabbo(buyer.Id);
        if (buyerUser == null || !Gamemap.TilesTouching(sellerUser.X, sellerUser.Y, buyerUser.X, buyerUser.Y))
            return StartResult.TooFar;
        if (SellerHasOpen(seller.Id))
            return StartResult.SellerBusy;
        Showing(buyer.Id, out var queued);
        if (queued >= MaxQueued)
            return StartResult.BuyerBusy;
        if (PartnershipUtility.PartnerOf(seller.Id) != 0)
            return StartResult.SellerPartnered;
        if (PartnershipUtility.PartnerOf(buyer.Id) != 0)
            return StartResult.BuyerPartnered;

        offer = new Offer
        {
            Id = Interlocked.Increment(ref _nextId),
            Kind = OfferKind.Proposal,
            SellerId = seller.Id,
            SellerName = seller.Username,
            BuyerId = buyer.Id,
            BuyerName = buyer.Username,
            Quantity = 1,
            RoomId = room.RoomId,
            ExpiresAt = DateTime.UtcNow.AddSeconds(LifetimeSeconds)
        };
        Live[offer.Id] = offer;
        sellerUser.CarryItem(ProposalHandItem);
        Push(buyer.Id);
        return StartResult.Ok;
    }

    // ---- answering -----------------------------------------------------------

    public static void Decline(int offerId, Habbo buyer)
    {
        if (buyer == null || !Live.TryGetValue(offerId, out var offer) || offer.BuyerId != buyer.Id)
            return;
        Live.TryRemove(offerId, out _);
        LowerHand(offer);
        Push(buyer.Id);
        // Bubble 5 for a refused proposal as for a refused sale: a no is the
        // card's ordinary answer, and only a yes earns the relationship bubble.
        Announce(buyer.CurrentRoom, buyer.Id, $"turns down {offer.SellerName}'s {offer.Noun}");
        PlusEnvironment.Game.ClientManager.GetClientByUserId(offer.SellerId)?
            .SendWhisper($"{buyer.Username} turned down your {offer.Noun}.");
    }

    /// <summary>The seller taking it back, from :offer cancel or from leaving.</summary>
    public static void CancelBySeller(int sellerId)
    {
        foreach (var offer in Live.Values.Where(entry => entry.SellerId == sellerId).ToList())
        {
            Live.TryRemove(offer.Id, out _);
            LowerHand(offer);
            Push(offer.BuyerId);
        }
    }

    /// <summary>Everything either side of a player, for a room leave or a disconnect.</summary>
    public static void Forget(int userId)
    {
        foreach (var offer in Live.Values.Where(entry => entry.SellerId == userId || entry.BuyerId == userId).ToList())
        {
            Live.TryRemove(offer.Id, out _);
            LowerHand(offer);
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
            LowerHand(offer);
            Push(buyer.Id);
            buyerSession!.SendWhisper($"{offer.SellerName} is no longer here.");
            return false;
        }
        if (offer.IsProposal)
            return AcceptProposal(offer, room, buyerSession!, buyer, sellerSession!, seller);
        var sellerUser = room.GetRoomUserManager()?.GetRoomUserByHabbo(seller.Id);
        if (!MedicalUtility.IsOnDutyHospitalStaff(seller.Id) ||
            (offer.Ware!.RequiredHandItem != 0 && sellerUser?.CarryItemId != offer.Ware.RequiredHandItem))
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
        Announce(room, buyer.Id, $"takes {Goods(offer)} from {offer.SellerName}{sum}");
        sellerSession?.SendWhisper($"{buyer.Username} accepted {Goods(offer)}{sum}.");
        return true;
    }

    /// <summary>
    /// A yes to :propose. Both are in the room (the caller checked); what the
    /// thirty seconds could have changed is whether either of them said yes to
    /// somebody else in the meantime, so that is asked again - and then asked a
    /// third time by the database, which is the check that cannot lose a race.
    ///
    /// Adjacency is NOT asked again. They were standing together when it was
    /// asked, they are in the same room now, and refusing a yes because one of
    /// them took a step would be the rule getting in the way of the scene.
    /// </summary>
    private static bool AcceptProposal(Offer offer, Room room, GameClient buyerSession, Habbo buyer,
        GameClient sellerSession, Habbo seller)
    {
        Live.TryRemove(offer.Id, out _);
        LowerHand(offer);
        Push(buyer.Id);

        if (PartnershipUtility.PartnerOf(buyer.Id) != 0)
        {
            buyerSession.SendWhisper("You are already in a partnership.");
            return false;
        }
        if (PartnershipUtility.PartnerOf(seller.Id) != 0 || !PartnershipUtility.TryPartner(seller.Id, buyer.Id))
        {
            buyerSession.SendWhisper($"{offer.SellerName} is already in a partnership.");
            sellerSession.SendWhisper($"{buyer.Username} could not accept - you are already in a partnership.");
            return false;
        }

        // The partner who asked no longer has anybody to ask, so any card they
        // are still waiting on from someone else goes.
        CancelProposalsTo(seller.Id);
        CancelProposalsTo(buyer.Id);

        Announce(room, buyer.Id, $"accepts {offer.SellerName}'s proposal", RelationshipBubble);
        sellerSession.SendWhisper($"{buyer.Username} accepted your proposal.");
        PartnershipUtility.PushRelationships(room, seller.Id);
        PartnershipUtility.PushRelationships(room, buyer.Id);
        return true;
    }

    /// <summary>
    /// Proposals waiting for somebody who has just partnered. Their answer can
    /// only be no now, so the card is taken off their screen rather than left
    /// to be tapped into a refusal.
    /// </summary>
    private static void CancelProposalsTo(int buyerId)
    {
        var dropped = false;
        foreach (var offer in Live.Values.Where(entry => entry.IsProposal && entry.BuyerId == buyerId).ToList())
        {
            if (!Live.TryRemove(offer.Id, out _))
                continue;
            LowerHand(offer);
            dropped = true;
            PlusEnvironment.Game.ClientManager.GetClientByUserId(offer.SellerId)?
                .SendWhisper($"Your proposal to {offer.BuyerName} was withdrawn - they are now in a partnership.");
        }
        if (dropped)
            Push(buyerId);
    }

    /// <summary>
    /// The goods as a person would say them: counted when they are things,
    /// named when they are an act. "3 Medkits", "a heal".
    /// </summary>
    public static string Goods(Offer offer) =>
        offer.IsProposal ? "a proposal" :
        offer.Ware!.TakesQuantity
            ? $"{TextHandling.GetNumber(offer.Quantity)} {offer.Label}"
            : $"a {offer.Ware.One.ToLowerInvariant()}";

    /// <summary>
    /// Bubble 5, wrapped in asterisks, from whoever did the thing.
    ///
    /// The room watched the offer being made, so it watches the answer too - a
    /// deal that is public in one direction and private in the other reads as
    /// half a scene. Same shape :give already uses: the client moves the
    /// opening marker ahead of the speaker's name, so this renders as
    /// "*Twist takes 3 Medkits from Ryan*".
    ///
    /// An accepted proposal is the one answer in another bubble: 16, the
    /// relationship bubble :hug and :kiss use, matching the :propose that asked.
    /// </summary>
    private static void Announce(Room? room, int habboId, string text, int bubble = OfferBubble)
    {
        var user = room?.GetRoomUserManager()?.GetRoomUserByHabbo(habboId);
        user?.OnChat(bubble, $"*{text}*", true);
    }

    /// <summary>The yellow bubble every offer, sale and answer is spoken in.</summary>
    public const int OfferBubble = 5;

    /// <summary>Bubble 16, the relationship bubble - :hug, :kiss, :propose and a yes to it.</summary>
    public const int RelationshipBubble = 16;

    // ---- the money -----------------------------------------------------------

    /// <summary>
    /// Hand to hand, and never the bank.
    ///
    /// Both sides are adjusted in the same breath with nothing between them
    /// that can fail - the shape :give already uses, and for the same reason:
    /// Habbo.Credits is the authority and the logout save writes it back
    /// absolutely, so the only residual hazard is a crash between the two
    /// saves. That is the hazard every credit change in this emulator carries,
    /// buying furniture included.
    ///
    /// The buyer having enough was checked a moment ago and is checked again
    /// here, because the moment was not free: they could have spent it at a
    /// vending machine while the card stood there.
    /// </summary>
    private static bool Charge(Habbo buyer, Habbo seller, Offer offer)
    {
        var amount = offer.Total;
        if (amount <= 0)
            return true;
        if (buyer.Credits < amount)
            return false;
        // A purse that wrapped past int.MaxValue would be a far worse bug than
        // a refused sale.
        if (seller.Credits > int.MaxValue - amount)
            return false;

        buyer.Credits -= (int)amount;
        seller.Credits += (int)amount;
        buyer.Client?.Send(new CreditBalanceComposer(buyer.Credits));
        seller.Client?.Send(new CreditBalanceComposer(seller.Credits));
        return true;
    }

    // ---- the goods -----------------------------------------------------------

    private static void Deliver(Room room, Offer offer, GameClient buyerSession, Habbo buyer)
    {
        if (offer.Ware!.Key == "heal")
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

    /// <summary>
    /// The proposer lowers their hand: every way a proposal ends comes through
    /// here, right after it leaves Live. Only if they are STILL holding it - a
    /// proposer who has since picked up a drink keeps the drink, because the
    /// answer to a proposal is no reason to knock something out of their hand.
    /// Read from where the proposer is now, not the room the card was made in:
    /// a proposal made in one room and withdrawn by leaving it has no hand to
    /// lower, and that is fine.
    /// </summary>
    private static void LowerHand(Offer offer)
    {
        if (!offer.IsProposal)
            return;
        var seller = PlusEnvironment.Game.ClientManager.GetClientByUserId(offer.SellerId)?.GetHabbo();
        var user = seller?.CurrentRoom?.GetRoomUserManager()?.GetRoomUserByHabbo(offer.SellerId);
        if (user != null && user.CarryItemId == ProposalHandItem)
            user.CarryItem(0);
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
            LowerHand(offer);
            Push(offer.BuyerId);
            PlusEnvironment.Game.ClientManager.GetClientByUserId(offer.SellerId)?
                .SendWhisper($"Your {offer.Noun} to {offer.BuyerName} expired.");
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

using System.Collections.Concurrent;
using Dapper;
using Plus.Communication.Packets.Outgoing.Users;
using Plus.HabboHotel.Corporations;
using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Rooms.Chat.Commands.User.Police;
using Plus.HabboHotel.Rooms.Offers;
using Plus.HabboHotel.Users;
using Plus.Utilities;

namespace Plus.HabboHotel.Rooms.Trading;

/// <summary>
/// pixelrp: trading backpack items (design: the "Trade Backpack" canvas).
///
/// Habbo's furni trade is gone: the avatar menu's Trade now sends a trade
/// request on the offer card (OfferState, kind Trade), and a yes opens this -
/// one trade window for each of the two players.
///
/// OFFERS ARE COUNTS OF AN ITEM, NOT SLOTS. "3 Medkits" stays true however the
/// backpack is rearranged; what is checked is that the carry slots still hold
/// that many. Equipped weapons are not carried, so they are not offerable until
/// put away, and police equipment is never offerable (PoliceUtility.PoliceGear).
///
/// NOTHING MOVES UNTIL BOTH CONFIRM. Offered items stay in the backpack and the
/// backpack is frozen while a trade is open (IsTrading gates use, discard, the
/// deposit box, :heal and :passive). Any change to either offer un-readies both
/// sides; both Ready, then both Confirm, and the swap is checked in full
/// against both backpacks before a single row is touched, so it happens whole
/// or not at all. Every completed trade is logged (rp_trades, migration 244) -
/// the City Panel's Trade History reads it.
///
/// A trade ends when either side cancels, leaves the room or logs out.
/// </summary>
public static class RpTrades
{
    public const int OfferSlots = 10;

    public const int ActionOffer = 1;
    public const int ActionTakeBack = 2;
    public const int ActionReady = 3;
    public const int ActionConfirm = 4;
    public const int ActionCancel = 5;

    public const int ResultCancelled = 0;
    public const int ResultComplete = 1;

    public sealed class Side
    {
        public int UserId;
        public string Name = "";
        public readonly Dictionary<string, int> Offer = new();
        public bool Ready;
        public bool Confirmed;
    }

    public sealed class Session
    {
        public int Id;
        public uint RoomId;
        public string RoomName = "";
        public readonly Side[] Sides = new Side[2];
        public readonly object Gate = new();

        public Side Mine(int userId) => Sides[0].UserId == userId ? Sides[0] : Sides[1];
        public Side Theirs(int userId) => Sides[0].UserId == userId ? Sides[1] : Sides[0];
    }

    private static int _nextId;
    private static readonly ConcurrentDictionary<int, Session> ByUser = new();

    public static bool IsTrading(int userId) => ByUser.ContainsKey(userId);

    /// <summary>The backpack is frozen while a trade is open: what to tell somebody who reaches into it.</summary>
    public const string FrozenMessage = "You cannot do that while you are trading.";

    public static bool Tradeable(string item) => !string.IsNullOrEmpty(item) && !PoliceUtility.PoliceGear.Contains(item);

    /// <summary>Why this player cannot trade right now, in their own words, or null.</summary>
    public static string? Refusal(GameClient? session)
    {
        var habbo = session?.GetHabbo();
        if (habbo == null)
            return "You are not here.";
        if (IsTrading(habbo.Id))
            return "You are already trading.";
        return StateRefusal(session!);
    }

    private static string? StateRefusal(GameClient session)
    {
        var habbo = session.GetHabbo();
        if (KnockedOut.Is(session))
            return "You are knocked out.";
        if (PoliceState.IsCuffed(habbo.Id))
            return "Your hands are cuffed.";
        if (JailState.IsJailed(habbo.Id))
            return "You cannot trade in jail.";
        if (habbo.TradingLockExpiry > UnixTimestamp.GetNow())
            return "You are banned from trading.";
        return null;
    }

    // ---- starting ------------------------------------------------------------

    /// <summary>A trade request was accepted: open the window for both.</summary>
    public static void Begin(GameClient first, GameClient second, Room room)
    {
        var session = new Session
        {
            Id = Interlocked.Increment(ref _nextId),
            RoomId = room.RoomId,
            RoomName = room.Name ?? ""
        };
        session.Sides[0] = new Side { UserId = first.GetHabbo().Id, Name = first.GetHabbo().Username };
        session.Sides[1] = new Side { UserId = second.GetHabbo().Id, Name = second.GetHabbo().Username };
        ByUser[session.Sides[0].UserId] = session;
        ByUser[session.Sides[1].UserId] = session;
        // The window shows the backpack beside the offers, and this is the one
        // moment it is certainly current.
        foreach (var client in new[] { first, second })
            client.Send(new RpInventoryComposer(client.GetHabbo().LoadRpInventory()));
        Push(session);
    }

    // ---- the window's buttons ---------------------------------------------

    public static void Act(GameClient client, int action, int slot, bool all, string item)
    {
        var habbo = client.GetHabbo();
        if (habbo == null || !ByUser.TryGetValue(habbo.Id, out var session))
        {
            client.Send(RpTradeComposer.Closed(ResultCancelled, ""));
            return;
        }
        if (action == ActionCancel)
        {
            End(habbo.Id, $"{habbo.Username} cancelled the trade.", "You cancelled the trade.");
            return;
        }
        if (action == ActionConfirm)
        {
            Confirm(client, session);
            return;
        }

        string notice;
        lock (session.Gate)
        {
            var mine = session.Mine(habbo.Id);
            switch (action)
            {
                case ActionOffer:
                    notice = Offer(habbo, session, mine, slot, all);
                    break;
                case ActionTakeBack:
                    notice = TakeBack(session, mine, item);
                    break;
                case ActionReady:
                    notice = Ready(session, mine);
                    break;
                default:
                    return;
            }
        }
        Push(session, habbo.Id, notice);
    }

    private static string Offer(Habbo habbo, Session session, Side mine, int slot, bool all)
    {
        if (slot < 1 || slot > Habbo.RpCarrySlots)
            return "";
        var entry = habbo.LoadRpInventory().FirstOrDefault(row => row.Slot == slot);
        if (string.IsNullOrEmpty(entry.Item))
            return "";
        if (!Tradeable(entry.Item))
            return "Police equipment cannot be traded.";
        mine.Offer.TryGetValue(entry.Item, out var offered);
        var available = CarryCount(habbo.Id, entry.Item) - offered;
        if (available <= 0)
            return "You have already offered all of those.";
        var amount = all ? Math.Min(entry.Count, available) : 1;
        var next = new Dictionary<string, int>(mine.Offer) { [entry.Item] = offered + amount };
        if (Cells(next) > OfferSlots)
            return "A trade holds ten slots.";
        mine.Offer[entry.Item] = offered + amount;
        Changed(session);
        return "";
    }

    private static string TakeBack(Session session, Side mine, string item)
    {
        if (!mine.Offer.TryGetValue(item ?? "", out var offered))
            return "";
        if (offered <= 1)
            mine.Offer.Remove(item!);
        else
            mine.Offer[item!] = offered - 1;
        Changed(session);
        return "";
    }

    private static string Ready(Session session, Side mine)
    {
        if (session.Sides.All(side => side.Offer.Count == 0))
            return "Offer something first.";
        var why = Check(session);
        if (why != null)
            return why;
        mine.Ready = true;
        return "";
    }

    /// <summary>Any change to either offer: neither side is ready any more.</summary>
    private static void Changed(Session session)
    {
        foreach (var side in session.Sides)
        {
            side.Ready = false;
            side.Confirmed = false;
        }
    }

    private static void Confirm(GameClient client, Session session)
    {
        var habbo = client.GetHabbo();
        bool swap;
        lock (session.Gate)
        {
            if (!session.Sides.All(side => side.Ready))
                return;
            session.Mine(habbo.Id).Confirmed = true;
            swap = session.Sides.All(side => side.Confirmed);
        }
        if (!swap)
        {
            Push(session);
            return;
        }
        Swap(session);
    }

    // ---- the swap ----------------------------------------------------------

    private static readonly object SwapLock = new();

    private static void Swap(Session session)
    {
        string? why;
        lock (SwapLock)
        {
            why = Check(session);
            if (why == null)
            {
                var a = session.Sides[0];
                var b = session.Sides[1];
                // Everything out first, then everything in - the order Check
                // worked it out in.
                Remove(a.UserId, a.Offer);
                Remove(b.UserId, b.Offer);
                Add(a.UserId, b.Offer);
                Add(b.UserId, a.Offer);
                Log(session);
            }
        }
        if (why != null)
        {
            lock (session.Gate)
                Changed(session);
            Push(session, 0, why);
            return;
        }

        foreach (var side in session.Sides)
            ByUser.TryRemove(side.UserId, out _);
        var room = PlusEnvironment.Game.RoomManager.TryGetRoom(session.RoomId, out var loaded) ? loaded : null;
        var announcer = room?.GetRoomUserManager()?.GetRoomUserByHabbo(session.Sides[0].UserId);
        announcer?.OnChat(OfferState.OfferBubble, $"*trades with {session.Sides[1].Name}*", true);
        foreach (var side in session.Sides)
        {
            var other = session.Theirs(side.UserId);
            var client = PlusEnvironment.Game.ClientManager.GetClientByUserId(side.UserId);
            if (client?.GetHabbo() == null)
                continue;
            client.Send(new RpInventoryComposer(client.GetHabbo().LoadRpInventory()));
            client.Send(RpTradeComposer.Closed(ResultComplete,
                $"You gave {Describe(side.Offer)}. You received {Describe(other.Offer)}."));
        }
    }

    /// <summary>
    /// Everything that has to be true for the swap, or why not: both still
    /// here and able, every offered item still carried, and each side's
    /// incoming items fitting once its outgoing ones have left.
    /// </summary>
    private static string? Check(Session session)
    {
        foreach (var side in session.Sides)
        {
            var client = PlusEnvironment.Game.ClientManager.GetClientByUserId(side.UserId);
            var habbo = client?.GetHabbo();
            if (habbo == null || habbo.CurrentRoom == null || habbo.CurrentRoom.RoomId != session.RoomId)
                return $"{side.Name} is not here.";
            var refusal = StateRefusal(client!);
            if (refusal != null)
                return side.Name + ": " + refusal;
        }
        foreach (var side in session.Sides)
        {
            var other = session.Theirs(side.UserId);
            var habbo = PlusEnvironment.Game.ClientManager.GetClientByUserId(side.UserId)!.GetHabbo();
            var inventory = habbo.LoadRpInventory();
            foreach (var (item, count) in side.Offer)
                if (Carried(inventory, item) < count)
                    return $"{side.Name} no longer has {count} × {ItemName(item)}.";
            if (!Fits(inventory, side.Offer, other.Offer, habbo.RpUnlockedSlots))
                return $"{side.Name}'s backpack cannot hold all of that.";
        }
        return null;
    }

    private static int CarryCount(int userId, string item) => Carried(RpInventoryStore.Load(userId), item);

    private static int Carried(List<(int Slot, string Item, int Count)> inventory, string item) =>
        inventory.Where(row => row.Item == item && row.Slot >= 1 && row.Slot <= Habbo.RpCarrySlots).Sum(row => row.Count);

    /// <summary>How many offer slots these counts fill: stacks of ten, weapons one each.</summary>
    public static int Cells(Dictionary<string, int> offer) =>
        offer.Sum(entry => RpWeapons.IsWeapon(entry.Key) ? entry.Value : (entry.Value + Habbo.RpStackCap - 1) / Habbo.RpStackCap);

    /// <summary>
    /// Whether `incoming` fits once `outgoing` has left - played out on a copy
    /// of the carry slots exactly as Remove and RpInventoryStore.Add will do it.
    /// </summary>
    private static bool Fits(List<(int Slot, string Item, int Count)> inventory, Dictionary<string, int> outgoing,
        Dictionary<string, int> incoming, int unlockedSlots)
    {
        var slots = inventory.Where(row => row.Slot >= 1 && row.Slot <= Habbo.RpCarrySlots)
            .ToDictionary(row => row.Slot, row => (row.Item, row.Count));
        foreach (var (item, count) in outgoing)
        {
            var left = count;
            foreach (var slot in slots.Where(pair => pair.Value.Item == item).Select(pair => pair.Key).OrderByDescending(slot => slot).ToList())
            {
                var take = Math.Min(left, slots[slot].Count);
                left -= take;
                if (slots[slot].Count - take <= 0)
                    slots.Remove(slot);
                else
                    slots[slot] = (item, slots[slot].Count - take);
                if (left == 0)
                    break;
            }
            if (left > 0)
                return false;
        }
        var all = inventory.Where(row => row.Slot > Habbo.RpCarrySlots).Select(row => row.Item).ToList();
        foreach (var (item, count) in incoming)
        {
            for (var i = 0; i < count; i++)
            {
                if (RpInventoryStore.IsOnePerPlayer(item) && (slots.Values.Any(row => row.Item == item) || all.Contains(item)))
                    return false;
                var stack = RpWeapons.IsWeapon(item)
                    ? 0
                    : slots.Where(pair => pair.Value.Item == item && pair.Value.Count < Habbo.RpStackCap)
                        .Select(pair => pair.Key).OrderBy(slot => slot).FirstOrDefault();
                if (stack > 0 && stack <= unlockedSlots)
                {
                    slots[stack] = (item, slots[stack].Count + 1);
                    continue;
                }
                var free = Enumerable.Range(1, unlockedSlots).FirstOrDefault(slot => !slots.ContainsKey(slot));
                if (free == 0)
                    return false;
                slots[free] = (item, 1);
            }
        }
        return true;
    }

    private static void Remove(int userId, Dictionary<string, int> outgoing)
    {
        foreach (var (item, count) in outgoing)
        {
            var left = count;
            var inventory = RpInventoryStore.Load(userId);
            foreach (var row in inventory.Where(row => row.Item == item && row.Slot >= 1 && row.Slot <= Habbo.RpCarrySlots).OrderByDescending(row => row.Slot))
            {
                var (_, removed) = RpInventoryStore.Discard(userId, row.Slot, Math.Min(left, row.Count));
                left -= removed;
                if (left <= 0)
                    break;
            }
        }
    }

    private static void Add(int userId, Dictionary<string, int> incoming)
    {
        var habbo = PlusEnvironment.Game.ClientManager.GetClientByUserId(userId)?.GetHabbo();
        var unlocked = habbo?.RpUnlockedSlots ?? Habbo.RpCarrySlotsBase;
        foreach (var (item, count) in incoming)
            for (var i = 0; i < count; i++)
                RpInventoryStore.Add(userId, item, unlocked);
    }

    // ---- ending --------------------------------------------------------------

    /// <summary>
    /// End this player's trade, if they are in one: `toOthers` is what the
    /// other side is told, `toThem` what this player is (empty: nothing).
    /// </summary>
    public static void End(int userId, string toOthers, string toThem = "")
    {
        if (!ByUser.TryGetValue(userId, out var session))
            return;
        foreach (var side in session.Sides)
            ByUser.TryRemove(side.UserId, out _);
        foreach (var side in session.Sides)
        {
            var client = PlusEnvironment.Game.ClientManager.GetClientByUserId(side.UserId);
            if (client?.GetHabbo() == null)
                continue;
            var text = side.UserId == userId ? toThem : toOthers;
            client.Send(RpTradeComposer.Closed(ResultCancelled,
                (text.Length > 0 ? text + " " : "") + "Nothing changed hands."));
        }
    }

    /// <summary>A player leaving the room or logging out.</summary>
    public static void Forget(int userId, bool loggedOut)
    {
        if (!ByUser.TryGetValue(userId, out var session))
            return;
        var name = session.Mine(userId).Name;
        End(userId, loggedOut ? $"{name} logged out." : $"{name} left the room.");
    }

    // ---- telling the clients -----------------------------------------------

    /// <summary>Both windows; `noticeFor` alone is also told `notice` (0: both are).</summary>
    private static void Push(Session session, int noticeFor = 0, string notice = "")
    {
        // Whether each side's offer fits the other backpack, worked out once
        // for both windows: the window says so before Ready is pressed.
        var fits = new Dictionary<int, bool>();
        foreach (var side in session.Sides)
            fits[side.UserId] = OfferFits(session, side);
        foreach (var side in session.Sides)
        {
            var client = PlusEnvironment.Game.ClientManager.GetClientByUserId(side.UserId);
            if (client?.GetHabbo() == null)
                continue;
            var other = session.Theirs(side.UserId);
            client.Send(new RpTradeComposer(session.Id, other.UserId, other.Name, side, other,
                fits[side.UserId], fits[other.UserId], (noticeFor == 0 || noticeFor == side.UserId) ? notice : ""));
        }
    }

    /// <summary>Whether what `giver` offers fits the other side's backpack once their own offer has left it.</summary>
    private static bool OfferFits(Session session, Side giver)
    {
        var receiver = session.Theirs(giver.UserId);
        if (giver.Offer.Count == 0)
            return true;
        var habbo = PlusEnvironment.Game.ClientManager.GetClientByUserId(receiver.UserId)?.GetHabbo();
        if (habbo == null)
            return false;
        return Fits(habbo.LoadRpInventory(), receiver.Offer, giver.Offer, habbo.RpUnlockedSlots);
    }

    private static string Describe(Dictionary<string, int> offer) =>
        offer.Count == 0 ? "nothing" : string.Join(", ", offer.Select(entry => $"{entry.Value} × {ItemName(entry.Key)}"));

    /// <summary>An item's name as :spawn lists it, else its key made readable (a clothing token).</summary>
    public static string ItemName(string item)
    {
        var known = Chat.Commands.Moderator.SpawnCommand.Spawnable.FirstOrDefault(entry => entry.ItemKey == item);
        return string.IsNullOrEmpty(known.Name) ? item.Replace('_', ' ') : known.Name;
    }

    // ---- the record ----------------------------------------------------------

    private static string Encode(Dictionary<string, int> offer) => string.Join(",", offer.Select(entry => $"{entry.Key}:{entry.Value}"));

    private static void Log(Session session)
    {
        try
        {
            using var connection = PlusEnvironment.DatabaseManager.Connection();
            connection.Execute(
                "INSERT INTO `rp_trades` (`user_a`, `name_a`, `user_b`, `name_b`, `room_id`, `room_name`, `gave_a`, `gave_b`, `created_at`) " +
                "VALUES (@userA, @nameA, @userB, @nameB, @roomId, @roomName, @gaveA, @gaveB, UNIX_TIMESTAMP())",
                new
                {
                    userA = session.Sides[0].UserId, nameA = session.Sides[0].Name,
                    userB = session.Sides[1].UserId, nameB = session.Sides[1].Name,
                    roomId = (int)session.RoomId, roomName = session.RoomName.Length > 64 ? session.RoomName[..64] : session.RoomName,
                    gaveA = Encode(session.Sides[0].Offer), gaveB = Encode(session.Sides[1].Offer)
                });
        }
        catch (Exception)
        {
            // The record is a record: a failed write never undoes a trade.
        }
    }
}

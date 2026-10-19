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
/// sides; both Ready, then both Confirm, and then the swap (Commit) is one
/// database transaction: both backpacks locked and read, every offered item
/// found and every incoming one given room from what was read under the lock,
/// and only then the rows written - whole, or not at all. Every completed trade
/// is logged (rp_trades, migration 244) - the City Panel's Trade History reads it.
///
/// A player is in one trade at most (Begin claims both of them or neither). A
/// trade ends when either side cancels, leaves the room or logs out - or when
/// the swap cannot be written, in which case nothing changed hands.
/// </summary>
public static class RpTrades
{
    private static readonly NLog.ILogger Logger = NLog.LogManager.GetLogger("Plus.HabboHotel.Rooms.Trading.RpTrades");

    /// <summary>The window's buttons, one at a time per player: a click every tenth of a second at most.</summary>
    private const long ActionGapMs = 100;

    private static readonly ConcurrentDictionary<int, long> LastAction = new();

    private sealed class Row
    {
        public int UserId { get; set; }
        public int Slot { get; set; }
        public string Item { get; set; } = "";
        public int Count { get; set; }
    }

    public const int OfferSlots = 10;

    public const int ActionOffer = 1;
    public const int ActionTakeBack = 2;
    public const int ActionReady = 3;
    public const int ActionConfirm = 4;
    public const int ActionCancel = 5;

    public const int ResultCancelled = 0;
    public const int ResultComplete = 1;

    /// <summary>
    /// Once both sides are Ready, Confirm only counts this long afterwards
    /// (Twist, 2026-10-17): a moment to look at the final offers before they
    /// can be swapped. The window counts it down on the button; this is the
    /// rule a hand-made Confirm cannot skip.
    /// </summary>
    public const int ConfirmDelaySeconds = 3;

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
        /// <summary>The swap has started (or the trade has ended): nothing more is taken from either side.</summary>
        public bool Closing;
        /// <summary>When both sides last became Ready; Confirm waits <see cref="ConfirmDelaySeconds"/> from it.</summary>
        public DateTime BothReadyAt;

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

    /// <summary>
    /// A trade request was accepted: open the window for both. False when
    /// either of them is already in a trade by now - each is claimed for this
    /// one only if neither is taken, so nobody is ever in two at once.
    /// </summary>
    public static bool Begin(GameClient first, GameClient second, Room room)
    {
        var session = new Session
        {
            Id = Interlocked.Increment(ref _nextId),
            RoomId = room.RoomId,
            RoomName = room.Name ?? ""
        };
        session.Sides[0] = new Side { UserId = first.GetHabbo().Id, Name = first.GetHabbo().Username };
        session.Sides[1] = new Side { UserId = second.GetHabbo().Id, Name = second.GetHabbo().Username };
        if (!ByUser.TryAdd(session.Sides[0].UserId, session))
            return false;
        if (!ByUser.TryAdd(session.Sides[1].UserId, session))
        {
            Release(session);
            return false;
        }
        // The window shows the backpack beside the offers, and this is the one
        // moment it is certainly current.
        foreach (var client in new[] { first, second })
            client.Send(new RpInventoryComposer(client.GetHabbo().LoadRpInventory()));
        Push(session);
        return true;
    }

    /// <summary>Let go of this trade's two players - only ever this trade's own claim on them.</summary>
    private static void Release(Session session)
    {
        foreach (var side in session.Sides)
        {
            ByUser.TryRemove(new KeyValuePair<int, Session>(side.UserId, session));
            LastAction.TryRemove(side.UserId, out _);
        }
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
            // The Cancel button, or the window's close: out loud over whoever
            // pressed it, like walking off (Twist, 2026-10-18).
            if (End(habbo.Id, $"{habbo.Username} cancelled the trade.", "You cancelled the trade."))
                SayCancelled(habbo);
            return;
        }
        // Every other button reads a backpack, so they are taken at a human
        // pace: anything faster than ActionGapMs since the last is dropped.
        var now = Environment.TickCount64;
        if (LastAction.TryGetValue(habbo.Id, out var last) && (now - last) < ActionGapMs)
            return;
        LastAction[habbo.Id] = now;
        if (action == ActionConfirm)
        {
            Confirm(client, session);
            return;
        }

        string notice;
        lock (session.Gate)
        {
            if (session.Closing)
                return;
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
        var inventory = habbo.LoadRpInventory();
        var entry = inventory.FirstOrDefault(row => row.Slot == slot);
        if (string.IsNullOrEmpty(entry.Item))
            return "";
        if (!Tradeable(entry.Item))
            return "Police equipment cannot be traded.";
        mine.Offer.TryGetValue(entry.Item, out var offered);
        var available = Carried(inventory, entry.Item) - offered;
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
        if (mine.Ready)
            return "";
        mine.Ready = true;
        if (session.Sides.All(side => side.Ready))
            session.BothReadyAt = DateTime.UtcNow;
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
            if (session.Closing || !session.Sides.All(side => side.Ready))
                return;
            // Too soon after both went Ready: the window's button is still
            // counting down, so only a hand-made Confirm lands here.
            if (DateTime.UtcNow < session.BothReadyAt.AddSeconds(ConfirmDelaySeconds))
                return;
            session.Mine(habbo.Id).Confirmed = true;
            swap = session.Sides.All(side => side.Confirmed);
            // One swap, however many Confirms arrive: a double click, or both
            // sides at once, must not run it twice.
            if (swap)
                session.Closing = true;
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
        try
        {
            lock (SwapLock)
                why = Commit(session);
        }
        catch (Exception e)
        {
            // Rolled back - the transaction never committed - so nothing moved.
            // Ended rather than left open: a swap that could not be written once
            // is not tried again, and nobody is left with a frozen backpack.
            Logger.Error(e, $"Trade {session.Id} ({session.Sides[0].Name} / {session.Sides[1].Name}) could not be written; ended with nothing changed.");
            Abort(session, "The trade could not be completed.");
            return;
        }
        if (why != null)
        {
            lock (session.Gate)
            {
                Changed(session);
                session.Closing = false;
            }
            // Somebody who left while the swap was being checked had their
            // leaving put off by it: end the trade for them now - quietly,
            // with no "left" line (Twist, 2026-10-18).
            var gone = session.Sides.FirstOrDefault(side =>
                PlusEnvironment.Game.ClientManager.GetClientByUserId(side.UserId)?.GetHabbo()?.CurrentRoom?.RoomId != session.RoomId);
            if (gone != null)
            {
                End(gone.UserId, "");
                return;
            }
            Push(session, 0, why);
            return;
        }

        Log(session);
        Release(session);
        var room = PlusEnvironment.Game.RoomManager.TryGetRoom(session.RoomId, out var loaded) ? loaded : null;
        var announcer = room?.GetRoomUserManager()?.GetRoomUserByHabbo(session.Sides[0].UserId);
        announcer?.OnChat(OfferState.OfferBubble, $"*trades with {session.Sides[1].Name}*", true);
        // Then what each side got, out loud in the blue action bubble, from
        // the one who got it (Twist, 2026-10-17). A side given nothing says
        // nothing.
        foreach (var side in session.Sides)
        {
            var incoming = session.Theirs(side.UserId).Offer;
            if (incoming.Count > 0)
                room?.GetRoomUserManager()?.GetRoomUserByHabbo(side.UserId)?
                    .OnChat(OfferState.TradeBubble, $"*receives {Received(incoming)}*", true);
        }
        foreach (var side in session.Sides)
        {
            var other = session.Theirs(side.UserId);
            var client = PlusEnvironment.Game.ClientManager.GetClientByUserId(side.UserId);
            if (client?.GetHabbo() == null)
                continue;
            var inventory = client.GetHabbo().LoadRpInventory();
            client.Send(new RpInventoryComposer(inventory));
            RpVape.Traded(client.GetHabbo(), inventory, side.Offer.GetValueOrDefault(RpVape.Item), other.Offer.GetValueOrDefault(RpVape.Item));
            client.Send(RpTradeComposer.Closed(ResultComplete,
                $"You gave {Describe(side.Offer)}. You received {Describe(other.Offer)}."));
        }
    }

    /// <summary>
    /// Everything that has to be true for the swap, or why not: both still
    /// here and able, every offered item still carried, and each side's
    /// incoming items fitting once its outgoing ones have left. What Ready
    /// asks; Commit asks it all again under the lock, from the locked rows.
    /// </summary>
    private static string? Check(Session session)
    {
        var why = Present(session, out var unlocked);
        if (why != null)
            return why;
        foreach (var side in session.Sides)
        {
            var other = session.Theirs(side.UserId);
            if (Plan(Slots(RpInventoryStore.Load(side.UserId)), side.Offer, other.Offer, unlocked[side.UserId], out var missing) == null)
                return Shortfall(side, missing);
        }
        return null;
    }

    /// <summary>Both sides online, in the trade's room and able to trade - and how many slots each has open, read now.</summary>
    private static string? Present(Session session, out Dictionary<int, int> unlocked)
    {
        unlocked = new Dictionary<int, int>();
        foreach (var side in session.Sides)
        {
            var client = PlusEnvironment.Game.ClientManager.GetClientByUserId(side.UserId);
            var habbo = client?.GetHabbo();
            if (habbo == null || habbo.CurrentRoom == null || habbo.CurrentRoom.RoomId != session.RoomId)
                return $"{side.Name} is not here.";
            var refusal = StateRefusal(client!);
            if (refusal != null)
                return side.Name + ": " + refusal;
            unlocked[side.UserId] = habbo.RpUnlockedSlots;
        }
        return null;
    }

    private static string Shortfall(Side side, string? missing) => missing != null
        ? $"{side.Name} no longer has {side.Offer[missing]} × {ItemName(missing)}."
        : $"{side.Name}'s backpack cannot hold all of that.";

    /// <summary>
    /// The swap itself, whole or not at all. Both backpacks are locked and
    /// read in one transaction (SELECT ... FOR UPDATE); each side's offer must
    /// be found in what was read there, and the other side's must fit into
    /// it, before a row is written - and every write must land on exactly the
    /// row that was read, or the transaction is thrown away. Null when done;
    /// otherwise why not, with nothing written.
    /// </summary>
    private static string? Commit(Session session)
    {
        var why = Present(session, out var unlocked);
        if (why != null)
            return why;
        var ids = session.Sides.Select(side => side.UserId).ToArray();
        using var connection = PlusEnvironment.DatabaseManager.Connection();
        connection.Open();
        using var transaction = connection.BeginTransaction();
        var rows = connection.Query<Row>(
            "SELECT `user_id` AS UserId, `slot` AS Slot, `item` AS Item, `count` AS `Count` FROM `user_rp_inventory` " +
            "WHERE `user_id` IN @ids ORDER BY `user_id`, `slot` FOR UPDATE",
            new { ids }, transaction).ToList();
        var plans = new Dictionary<int, (Dictionary<int, (string Item, int Count)> Before, Dictionary<int, (string Item, int Count)> After)>();
        foreach (var side in session.Sides)
        {
            var before = rows.Where(row => row.UserId == side.UserId).ToDictionary(row => row.Slot, row => (row.Item, row.Count));
            var after = Plan(before, side.Offer, session.Theirs(side.UserId).Offer, unlocked[side.UserId], out var missing);
            if (after == null)
            {
                transaction.Rollback();
                // Ready found it all here a moment ago, and the backpack has
                // been frozen since: worth a line in the log.
                Logger.Warn($"Trade {session.Id}: {side.Name}'s backpack no longer matched the trade at the swap ({(missing != null ? "missing " + missing : "no room")}); nothing changed.");
                return Shortfall(side, missing);
            }
            plans[side.UserId] = (before, after);
        }
        foreach (var (userId, (before, after)) in plans)
            Write(connection, transaction, userId, before, after);
        transaction.Commit();
        return null;
    }

    /// <summary>One backpack from what was read to what it becomes, a row at a time, each write on exactly the row it expects.</summary>
    private static void Write(System.Data.IDbConnection connection, System.Data.IDbTransaction transaction, int userId,
        Dictionary<int, (string Item, int Count)> before, Dictionary<int, (string Item, int Count)> after)
    {
        foreach (var (slot, was) in before)
        {
            if (!after.TryGetValue(slot, out var now))
                Exactly(connection.Execute(
                    "DELETE FROM `user_rp_inventory` WHERE `user_id` = @userId AND `slot` = @slot AND `item` = @item AND `count` = @count LIMIT 1",
                    new { userId, slot, item = was.Item, count = was.Count }, transaction));
            else if (now != was)
                Exactly(connection.Execute(
                    "UPDATE `user_rp_inventory` SET `item` = @item, `count` = @count " +
                    "WHERE `user_id` = @userId AND `slot` = @slot AND `item` = @wasItem AND `count` = @wasCount LIMIT 1",
                    new { userId, slot, item = now.Item, count = now.Count, wasItem = was.Item, wasCount = was.Count }, transaction));
        }
        foreach (var (slot, now) in after)
            if (!before.ContainsKey(slot))
                Exactly(connection.Execute(
                    "INSERT INTO `user_rp_inventory` (`user_id`, `slot`, `item`, `count`) VALUES (@userId, @slot, @item, @count)",
                    new { userId, slot, item = now.Item, count = now.Count }, transaction));
    }

    private static void Exactly(int affected)
    {
        if (affected != 1)
            throw new InvalidOperationException($"A backpack row did not match what the trade read under its lock ({affected} rows).");
    }

    /// <summary>A swap that could not be written: the trade ends for both, nothing having moved.</summary>
    private static void Abort(Session session, string text)
    {
        lock (session.Gate)
            session.Closing = true;
        Release(session);
        foreach (var side in session.Sides)
            PlusEnvironment.Game.ClientManager.GetClientByUserId(side.UserId)?
                .Send(RpTradeComposer.Closed(ResultCancelled, text + " Nothing changed hands."));
    }

    private static Dictionary<int, (string Item, int Count)> Slots(List<(int Slot, string Item, int Count)> inventory) =>
        inventory.GroupBy(row => row.Slot).ToDictionary(group => group.Key, group => (group.First().Item, group.First().Count));

    private static int Carried(List<(int Slot, string Item, int Count)> inventory, string item) =>
        inventory.Where(row => row.Item == item && row.Slot >= 1 && row.Slot <= Habbo.RpCarrySlots).Sum(row => row.Count);

    /// <summary>How many offer slots these counts fill: stacks of ten, weapons one each.</summary>
    public static int Cells(Dictionary<string, int> offer) =>
        offer.Sum(entry => RpWeapons.IsWeapon(entry.Key) ? entry.Value : (entry.Value + Habbo.RpStackCap - 1) / Habbo.RpStackCap);

    /// <summary>Whether `incoming` fits once `outgoing` has left (Plan).</summary>
    private static bool Fits(List<(int Slot, string Item, int Count)> inventory, Dictionary<string, int> outgoing,
        Dictionary<string, int> incoming, int unlockedSlots) =>
        Plan(Slots(inventory), outgoing, incoming, unlockedSlots, out _) != null;

    /// <summary>
    /// One backpack (every slot, the Weapon slot included) after the trade:
    /// `outgoing` taken from the carry slots, highest first, then `incoming`
    /// put in one at a time the way RpInventoryStore.Add places things - onto
    /// the first part-used stack of the same thing if it is open, else the
    /// first free slot; weapons never stack, and a one-per-player item already
    /// held anywhere is refused. Null when it cannot be done, with `missing`
    /// the offered item that is not all there (null when it is a matter of room).
    /// The one rule for the window's fit, Ready and the swap itself.
    /// </summary>
    private static Dictionary<int, (string Item, int Count)>? Plan(Dictionary<int, (string Item, int Count)> before,
        Dictionary<string, int> outgoing, Dictionary<string, int> incoming, int unlockedSlots, out string? missing)
    {
        missing = null;
        var slots = new Dictionary<int, (string Item, int Count)>(before);
        bool IsCarry(int slot) => slot >= 1 && slot <= Habbo.RpCarrySlots;
        foreach (var (item, count) in outgoing)
        {
            var left = count;
            foreach (var slot in slots.Where(pair => IsCarry(pair.Key) && pair.Value.Item == item)
                         .Select(pair => pair.Key).OrderByDescending(slot => slot).ToList())
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
            {
                missing = item;
                return null;
            }
        }
        foreach (var (item, count) in incoming)
        {
            for (var i = 0; i < count; i++)
            {
                if (RpInventoryStore.IsOnePerPlayer(item) && slots.Values.Any(row => row.Item == item))
                    return null;
                var stack = RpWeapons.IsWeapon(item)
                    ? 0
                    : slots.Where(pair => IsCarry(pair.Key) && pair.Value.Item == item && pair.Value.Count < Habbo.RpStackCap)
                        .Select(pair => pair.Key).OrderBy(slot => slot).FirstOrDefault();
                if (stack > 0 && stack <= unlockedSlots)
                {
                    slots[stack] = (item, slots[stack].Count + 1);
                    continue;
                }
                var free = Enumerable.Range(1, unlockedSlots).FirstOrDefault(slot => !slots.ContainsKey(slot));
                if (free == 0)
                    return null;
                slots[free] = (item, 1);
            }
        }
        return slots;
    }

    // ---- ending --------------------------------------------------------------

    /// <summary>
    /// End this player's trade, if they are in one: `toOthers` is what the
    /// other side is told, `toThem` what this player is (empty: nothing).
    /// </summary>
    /// <summary>End `userId`'s trade for both sides; false when there was none to end (or a swap is under way).</summary>
    public static bool End(int userId, string toOthers, string toThem = "")
    {
        if (!ByUser.TryGetValue(userId, out var session))
            return false;
        lock (session.Gate)
        {
            // A swap under way finishes; leaving mid-swap does not undo it.
            if (session.Closing)
                return false;
            session.Closing = true;
        }
        Release(session);
        // Nothing to say at all (a logout - Twist, 2026-10-18): the windows
        // just close, with no line and no whisper.
        var quiet = (toOthers.Length == 0) && (toThem.Length == 0);
        foreach (var side in session.Sides)
        {
            var client = PlusEnvironment.Game.ClientManager.GetClientByUserId(side.UserId);
            if (client?.GetHabbo() == null)
                continue;
            var text = side.UserId == userId ? toThem : toOthers;
            // No whisper to either side, whatever ended it (Twist, 2026-10-18):
            // the window closing is the whole of it - and walking off has its
            // "*cancels trade*" bubble (CancelByWalking).
            client.Send(RpTradeComposer.Closed(ResultCancelled,
                quiet ? "" : (text.Length > 0 ? text + " " : "") + "Nothing changed hands."));
        }
        return true;
    }

    /// <summary>
    /// A trader clicked to walk (MoveAvatarEvent): walking off ends the trade
    /// for both, and the room sees it in the blue action bubble (Twist,
    /// 2026-10-17). Only the player's own click - a push or a pull from
    /// somebody else does not end their trade for them.
    /// </summary>
    public static void CancelByWalking(GameClient client)
    {
        var habbo = client?.GetHabbo();
        if (habbo == null || !IsTrading(habbo.Id))
            return;
        if (!End(habbo.Id, $"{habbo.Username} cancelled the trade.", "You cancelled the trade."))
            return;
        SayCancelled(habbo);
    }

    /// <summary>The blue action bubble over whoever cancelled - the Cancel button, the window's close, or walking off.</summary>
    private static void SayCancelled(Habbo habbo) =>
        habbo.CurrentRoom?.GetRoomUserManager()?.GetRoomUserByHabbo(habbo.Id)?
            .OnChat(OfferState.TradeBubble, "*cancels trade*", true);

    /// <summary>
    /// A player leaving the room or logging out (Twist, 2026-10-18). A logout
    /// ends the trade without a word - the other window just closes. Leaving
    /// the room is cancelling it, and reads exactly like the Cancel button.
    /// </summary>
    public static void Forget(int userId, bool loggedOut)
    {
        if (!ByUser.TryGetValue(userId, out var session))
            return;
        var name = session.Mine(userId).Name;
        if (loggedOut)
            End(userId, "");
        else
            End(userId, $"{name} cancelled the trade.", "You cancelled the trade.");
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

    /// <summary>
    /// What a side got, for its room bubble: "Medkit ×2, Snack ×3 and an Axe" -
    /// the count only past one, as the trade window's own labels put it, and
    /// a single thing with its article (Twist, 2026-10-18): "a Passive Smoothie".
    /// </summary>
    private static string Received(Dictionary<string, int> offer)
    {
        var names = offer.Select(entry => entry.Value > 1 ? $"{ItemName(entry.Key)} ×{entry.Value}" : WithArticle(ItemName(entry.Key))).ToList();
        return names.Count <= 1 ? string.Concat(names) : $"{string.Join(", ", names.Take(names.Count - 1))} and {names[^1]}";
    }

    private static string WithArticle(string name) =>
        ("AEIOU".IndexOf(char.ToUpperInvariant(name.FirstOrDefault())) >= 0 ? "an " : "a ") + name;

    /// <summary>An item's name as :spawn lists it, "Clothing Token" for a Clothing Store token, else its key made readable.</summary>
    public static string ItemName(string item)
    {
        if (item.StartsWith(Communication.Packets.Incoming.Users.RpBuyClothingEvent.TokenPrefix))
            return "Clothing Token";
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

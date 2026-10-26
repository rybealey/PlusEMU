using System.Collections.Concurrent;
using Dapper;
using Plus.Communication.Packets.Outgoing.Inventory.Purse;
using Plus.Communication.Packets.Outgoing.Rooms.Chat;
using Plus.Communication.Packets.Outgoing.Users;
using Plus.HabboHotel.CityPanel;
using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Items;
using Plus.HabboHotel.Rooms.Chat.Commands.User.Police;
using Plus.HabboHotel.Rooms.PathFinding;
using Plus.HabboHotel.Rooms.Trading;
using Plus.HabboHotel.Users.Accounts;
using Plus.HabboHotel.Users.Banking;

namespace Plus.HabboHotel.Rooms;

/// <summary>
/// pixelrp: searching a bin (Twist, 2026-10-18). The PixelRP Bin, and any
/// furni given the bin_search behaviour, in a PixelRP-owned room.
///
/// Double-click it: once the searcher stands still beside it (walking up to it
/// first, when they are not), ten seconds of rummaging, flies (effect 10)
/// over the searcher's head and "*starts searching through the bin*" over it,
/// then one find from the City Panel's table (BinRewards) said out loud the
/// same way. The bin then shows its open, searched state (1) for fifteen
/// minutes and nobody can search it until it shuts again.
///
/// The search stops - "You stopped interacting with the item." - on a click to
/// walk, on being moved off the tile it started on (a pull, a push, a pepper
/// spray stumble, an escort), on a hit or a slap, and on being cuffed,
/// stunned, knocked out or leaving. The flies come off and whatever effect was
/// underneath goes back on.
///
/// An account - every character on it together - finishes at most DailyCap
/// searches in any 24 hours, counted from rp_bin_search_log, which keeps one
/// row per finished search, whatever it found. Only staff put searchable bins
/// down in a PixelRP room (MayPlace).
///
/// Starting, finishing and stopping a search each happen whole, one at a time
/// (Gate): a bin is searched once per fifteen minutes, an account's count is
/// exact, and a search ends exactly one way.
///
/// Both clocks run on the bin's own update ticks (Item.ProcessUpdates): once a
/// cycle while someone searches, and once at the end of the fifteen minutes.
/// When a bin was searched lives here, by item id, rather than on the room or
/// in the bin's state, so a room emptying and loading again does not shut its
/// bins early and nothing that sets a furni's state can reopen one; only a
/// restart forgets, and then an open bin shuts fifteen minutes after its room
/// loads (OnLoaded).
///
/// Bins anywhere else open and shut on a double-click, as before.
/// </summary>
public static class BinSearch
{
    public const int FliesEffectId = 10;
    public const long SearchMs = 10_000;
    public const long ResetMs = 15 * 60 * 1000;
    public const int DailyCap = 5;
    public const string StoppedMessage = "You stopped interacting with the item.";

    // A room cycle is half a second; the reset is re-checked against the
    // clock when it comes due, so a slow room only ever shuts a bin late.
    private const int CycleMs = 500;
    private const int CapWindowSeconds = 24 * 60 * 60;
    // A search past this has lost its bin's tick (its room unloaded under it).
    private const long StaleMs = SearchMs + 5_000;
    // One attempt per player in this long: a double-click is one attempt, and
    // a flood of them gets no further than the first line of Start.
    private const long AttemptGapMs = 500;
    // Five seconds after a search ends - found or stopped - before the same
    // player can start another (Twist, 2026-10-18).
    public const long CooldownMs = 5_000;
    // A double-click still waiting for its player to stand still this long
    // has been forgotten; the walk-up is long over.
    private const long HeldMs = 60_000;

    private sealed class Search
    {
        public int UserId;
        public int AccountId;
        public uint ItemId;
        public uint RoomId;
        public int X;
        public int Y;
        public long StartedAt;
        public int EffectBefore;
    }

    // A double-click waiting for its player to stand still beside the bin
    // (Start, TickHeld).
    private sealed class Held
    {
        public uint ItemId;
        // Their walks so far, to tell when they walk again (TickHeld).
        public int WalkRequests;
        // Sent up to the bin already, so standing still out of reach again
        // lets it go rather than walking them up a second time.
        public bool WalkedUp;
        public long At;
    }

    private static readonly object Gate = new();
    private static readonly ConcurrentDictionary<int, Search> ByUser = new();
    private static readonly ConcurrentDictionary<uint, int> SearcherOf = new();
    private static readonly ConcurrentDictionary<uint, long> SearchedAt = new();
    private static readonly ConcurrentDictionary<int, long> LastAttempt = new();
    private static readonly ConcurrentDictionary<int, long> LastEnded = new();
    private static readonly ConcurrentDictionary<int, Held> HeldBy = new();

    /// <summary>Whole seconds before this player may start another search; 0 when they may now.</summary>
    private static int CooldownLeft(int userId)
    {
        if (!LastEnded.TryGetValue(userId, out var at))
            return 0;
        var left = CooldownMs - (Now - at);
        if (left > 0)
            return (int)Math.Ceiling(left / 1000.0);
        LastEnded.TryRemove(new KeyValuePair<int, long>(userId, at));
        return 0;
    }

    private static long Now => Environment.TickCount64;

    public static bool IsSearchable(Item item, Room? room = null) =>
        item?.Definition?.InteractionType == InteractionType.BinSearch && PixelRpRooms.IsPixelRpRoom(room ?? item.GetRoom());

    private static bool IsSearched(uint itemId) =>
        SearchedAt.TryGetValue(itemId, out var at) && (Now - at) < ResetMs;

    /// <summary>
    /// Whether this furni may be put down here by this person: a searchable
    /// bin goes into a PixelRP room only from staff. Every new floor placement
    /// passes through here (RoomItemHandling.SetFloorItem), a gift unwrapped
    /// in the room as much as one put down from the inventory.
    /// </summary>
    public static bool MayPlace(Room room, Item item, GameClient? session) =>
        item?.Definition?.InteractionType != InteractionType.BinSearch
        || !PixelRpRooms.IsPixelRpRoom(room)
        || PixelRpRooms.IsStaff(session?.GetHabbo());

    /// <summary>
    /// A double-click on a searchable bin. Standing still beside it, the
    /// search starts now. Otherwise the double-click is held (Hold) until they
    /// stand still beside it: the first click of a double-click is also a
    /// click on the floor under the mouse - behind the bin, wherever it stands
    /// on open floor - so the use often comes in with that walk under way.
    /// Started mid-step, the search stopped again on the next tile; turned
    /// away, the double-click did nothing.
    /// </summary>
    public static void Start(GameClient session, Item item)
    {
        var habbo = session?.GetHabbo();
        var room = item.GetRoom();
        var user = room?.GetRoomUserManager().GetRoomUserByHabbo(habbo?.Id ?? 0);
        if (session == null || habbo == null || room == null || user == null)
            return;
        var now = Now;
        if (LastAttempt.TryGetValue(habbo.Id, out var last) && (now - last) < AttemptGapMs)
            return;
        LastAttempt[habbo.Id] = now;

        var moving = IsMoving(user);
        var beside = Gamemap.TilesTouching(user.X, user.Y, item.GetX, item.GetY);
        if (beside && !moving)
            Face(user, item);
        // Before any walking: no point walking up to a bin they can't search.
        if (Refused(session, habbo.Id, item))
            return;

        // Their own walk ends beside the bin: the search starts when it does.
        if (moving && Gamemap.TilesTouching(user.GoalX, user.GoalY, item.GetX, item.GetY))
        {
            Hold(habbo.Id, user, item, walkedUp: false);
            return;
        }
        // Like the vending machines: walk up to it first.
        if (moving || !beside)
        {
            user.MoveTo(item.SquareInFront);
            Hold(habbo.Id, user, item, walkedUp: true);
            return;
        }
        HeldBy.TryRemove(habbo.Id, out _);
        Begin(session, item, room, user, fromTick: false);
    }

    // The answers that need nothing from the database, asked before anyone
    // walks anywhere. All but the cooldown are asked again under the Gate in
    // Begin, which is where they count.
    private static bool Refused(GameClient session, int userId, Item item)
    {
        var now = Now;
        if (ByUser.TryGetValue(userId, out var mine) && (now - mine.StartedAt) <= StaleMs)
        {
            if (mine.ItemId != item.Id)
                session.SendWhisper("You're already searching a bin.");
            return true;
        }
        // The hotel's one cooldown whisper, as :hit and the gym say it (Twist,
        // 2026-10-18): seconds still to wait over the whole, [5/5] down to [1/5].
        var cooldown = CooldownLeft(userId);
        if (cooldown > 0)
        {
            session.SendWhisper($"Cooldown [{cooldown}/{CooldownMs / 1000}]");
            return true;
        }
        if (IsSearched(item.Id))
        {
            session.SendWhisper("This bin has already been searched.");
            return true;
        }
        if (SearcherOf.TryGetValue(item.Id, out var busy) && busy != userId
            && ByUser.TryGetValue(busy, out var busyWith) && busyWith.ItemId == item.Id && (now - busyWith.StartedAt) <= StaleMs)
        {
            session.SendWhisper("Someone is already searching this bin.");
            return true;
        }
        return false;
    }

    /// <summary>
    /// The search itself, for a player standing still beside the bin who got
    /// past Refused: the refusals that need more than memory, then the search
    /// taken under the Gate. From the bin's own tick (a held double-click) it
    /// re-arms with the counter alone, as Tick does.
    /// </summary>
    private static void Begin(GameClient session, Item item, Room room, RoomUser user, bool fromTick)
    {
        var habbo = session.GetHabbo();
        if (PoliceState.IsCuffed(habbo.Id))
        {
            session.SendWhisper("Your hands are cuffed.");
            return;
        }
        if (KnockedOut.Refuse(session))
            return;
        if (PoliceState.IsStunned(habbo.Id) || PoliceState.IsBeingEscorted(habbo.Id))
        {
            session.SendWhisper(KnockedOut.Refusal);
            return;
        }
        if (JailState.IsJailed(habbo.Id))
        {
            session.SendWhisper("You can't do that in jail.");
            return;
        }
        // The backpack is frozen while a trade is open.
        if (RpTrades.IsTrading(habbo.Id))
        {
            session.SendWhisper(RpTrades.FrozenMessage);
            return;
        }
        // Whatever turns up needs somewhere to go - like the snack machine,
        // no free slot means no search.
        if (habbo.LoadRpInventory().Count(entry => entry.Slot >= 1 && entry.Slot <= habbo.RpUnlockedSlots) >= habbo.RpUnlockedSlots)
        {
            session.SendWhisper("Your backpack is full.");
            return;
        }

        var accountId = AccountUtility.RootOf(habbo.Id);
        lock (Gate)
        {
            if (ByUser.TryGetValue(habbo.Id, out var current))
            {
                if (Now - current.StartedAt <= StaleMs)
                {
                    if (current.ItemId != item.Id)
                        session.SendWhisper("You're already searching a bin.");
                    return;
                }
                Release(current);
            }
            if (SearcherOf.TryGetValue(item.Id, out var searcher))
            {
                if (ByUser.TryGetValue(searcher, out var theirs) && theirs.ItemId == item.Id && (Now - theirs.StartedAt) <= StaleMs)
                {
                    session.SendWhisper("Someone is already searching this bin.");
                    return;
                }
                if (theirs != null && theirs.ItemId == item.Id)
                    Release(theirs);
                SearcherOf.TryRemove(item.Id, out _);
            }
            if (IsSearched(item.Id))
            {
                session.SendWhisper("This bin has already been searched.");
                return;
            }
            // Finished searches on record, and any another character on the
            // account has under way right now.
            var used = FinishedToday(accountId) + ByUser.Values.Count(other => other.AccountId == accountId);
            if (used >= DailyCap)
            {
                session.SendWhisper($"You can only search {DailyCap} bins every 24 hours.");
                return;
            }

            // The search before the bin's pointer to it: the tick reads them
            // without the Gate, and must never find a searcher with no search.
            ByUser[habbo.Id] = new Search
            {
                UserId = habbo.Id, AccountId = accountId, ItemId = item.Id, RoomId = room.Id, X = user.X, Y = user.Y,
                StartedAt = Now, EffectBefore = habbo.Effects?.CurrentEffect ?? 0
            };
            SearcherOf[item.Id] = habbo.Id;
        }

        user.ApplyEffect(FliesEffectId);
        Say(room, user, "*starts searching through the bin*");
        // Their own card above the chat bar: the bin's icon and the countdown.
        session.Send(new RpBinSearchComposer(true, item.Definition.SpriteId, (int)SearchMs, (int)SearchMs));
        if (fromTick)
            item.RequestUpdate(1, false);
        else
            Wake(item);
    }

    // Hold a double-click until its player stands still beside the bin; the
    // bin ticks for it until then (TickHeld). A newer one replaces it.
    private static void Hold(int userId, RoomUser user, Item item, bool walkedUp)
    {
        HeldBy[userId] = new Held { ItemId = item.Id, WalkRequests = user.WalkRequests, WalkedUp = walkedUp, At = Now };
        Wake(item);
    }

    // The bin's tick, asked for from outside it (a packet). One already
    // counting down only needs its count brought in: queued a second time it
    // would tick twice a cycle.
    private static void Wake(Item item)
    {
        if (item.UpdateCounter > 0)
            item.UpdateCounter = 1;
        else
            item.RequestUpdate(1, true);
    }

    private static void Face(RoomUser user, Item item) =>
        user.SetRot(Rotation.Calculate(user.X, user.Y, item.GetX, item.GetY), false);

    // Walking, or waiting on the room's beat to set off - which
    // RoomUser.IsWalking alone does not show.
    private static bool IsMoving(RoomUser user) =>
        user.IsWalking || Movement.MovementV2Bridge.IsWalkingOrWaiting(user);

    /// <summary>
    /// Stop this player's search, if they have one, and tell them. For the
    /// moments the tick would only notice late: a click to walk, a hit or a
    /// slap, and anything about to take over the effect slot (cuffs, a stun,
    /// pepper spray) - which must find the flies already gone, or it would
    /// keep them to hand back afterwards.
    /// </summary>
    public static void Interrupt(int userId)
    {
        if (ByUser.ContainsKey(userId))
            Stop(userId, true);
    }

    /// <summary>A bin picked up: whoever was searching it, or on their way to, has nothing left to search.</summary>
    public static void OnRemoved(Item item)
    {
        if (SearcherOf.TryGetValue(item.Id, out var userId))
            Stop(userId, true);
        foreach (var (heldBy, held) in HeldBy)
        {
            if (held.ItemId == item.Id)
                HeldBy.TryRemove(new KeyValuePair<int, Held>(heldBy, held));
        }
    }

    /// <summary>
    /// The bin's own update tick: a search in progress, or the fifteen minutes
    /// running out, and the double-clicks held on it. Called from inside the
    /// room's update pass (Item.ProcessUpdates), so it re-arms with the
    /// counter alone - the pass keeps an item with a count left for the next
    /// cycle. Queueing it again from here would put it straight back into the
    /// pass that is running.
    /// </summary>
    public static void Tick(Item item)
    {
        TickSearch(item);
        // Every cycle while one waits, whatever the search side asked for -
        // the fifteen minutes are read off the clock, so ticking early only
        // counts them again.
        if (TickHeld(item))
            item.RequestUpdate(1, false);
    }

    /// <summary>
    /// The double-clicks held on this bin (Start). Standing still beside it,
    /// the search starts; standing still out of reach, they are walked up to
    /// it - once. Walking off somewhere else, being moved off, leaving the
    /// room or a minute gone lets it go. True while one is still on its way.
    /// </summary>
    private static bool TickHeld(Item item)
    {
        var waiting = false;
        foreach (var (userId, held) in HeldBy)
        {
            if (held.ItemId != item.Id)
                continue;
            var client = PlusEnvironment.Game.ClientManager.GetClientByUserId(userId);
            var room = item.GetRoom();
            var user = room?.GetRoomUserManager().GetRoomUserByHabbo(userId);
            if (client?.GetHabbo() == null || room == null || user == null || Now - held.At > HeldMs)
            {
                HeldBy.TryRemove(new KeyValuePair<int, Held>(userId, held));
                continue;
            }
            // Walked again since. Towards the bin - another click on it, the
            // first click of a double-click again - it still stands; anywhere
            // else it is let go.
            if (user.WalkRequests != held.WalkRequests)
            {
                if (!Gamemap.TilesTouching(user.GoalX, user.GoalY, item.GetX, item.GetY))
                {
                    HeldBy.TryRemove(new KeyValuePair<int, Held>(userId, held));
                    continue;
                }
                held.WalkRequests = user.WalkRequests;
            }
            if (IsMoving(user))
            {
                waiting = true;
                continue;
            }
            if (Gamemap.TilesTouching(user.X, user.Y, item.GetX, item.GetY))
            {
                HeldBy.TryRemove(new KeyValuePair<int, Held>(userId, held));
                Face(user, item);
                if (!Refused(client, userId, item))
                    Begin(client, item, room, user, fromTick: true);
                continue;
            }
            if (held.WalkedUp)
            {
                HeldBy.TryRemove(new KeyValuePair<int, Held>(userId, held));
                continue;
            }
            // Their click took them past it. Walking up is part of the use, so
            // it is their own walking off, as from the packet (OwnWalk.Use).
            OwnWalk.Use(client, room, () => user.MoveTo(item.SquareInFront));
            held.WalkedUp = true;
            held.WalkRequests = user.WalkRequests;
            waiting = true;
        }
        return waiting;
    }

    private static void TickSearch(Item item)
    {
        if (SearcherOf.TryGetValue(item.Id, out var userId))
        {
            if (!ByUser.TryGetValue(userId, out var search) || search.ItemId != item.Id)
            {
                lock (Gate)
                {
                    if (!ByUser.TryGetValue(userId, out var again) || again.ItemId != item.Id)
                    {
                        SearcherOf.TryRemove(new KeyValuePair<uint, int>(item.Id, userId));
                        return;
                    }
                }
                item.RequestUpdate(1, false);
                return;
            }
            var client = PlusEnvironment.Game.ClientManager.GetClientByUserId(userId);
            var habbo = client?.GetHabbo();
            var user = item.GetRoom()?.GetRoomUserManager().GetRoomUserByHabbo(userId);
            if (client == null || habbo == null || user == null || habbo.CurrentRoom?.Id != search.RoomId)
            {
                Stop(userId, true);
                return;
            }
            if (user.X != search.X || user.Y != search.Y || user.RpKnockedOut
                || PoliceState.IsCuffed(userId) || PoliceState.IsStunned(userId) || PoliceState.IsBeingEscorted(userId)
                || RpTrades.IsTrading(userId))
            {
                Stop(userId, true);
                return;
            }
            if (Now - search.StartedAt >= SearchMs)
            {
                Finish(client, item, user, search);
                return;
            }
            item.RequestUpdate(1, false);
            return;
        }

        if (!SearchedAt.TryGetValue(item.Id, out var at))
            return;
        var left = ResetMs - (Now - at);
        if (left > 0)
        {
            item.RequestUpdate((int)(left / CycleMs) + 1, false);
            return;
        }
        SearchedAt.TryRemove(item.Id, out _);
        SetState(item, "0");
    }

    /// <summary>
    /// A bin coming into a room - loaded with it, or placed. One searched in
    /// the last fifteen minutes is shown open until its time is up. One that
    /// loads open with no search on record was left that way by a restart and
    /// gets the full fifteen from now; one PLACED open was only opened by
    /// hand somewhere, and is shut. Anywhere but a PixelRP room its open lid
    /// is only a lid.
    ///
    /// A room loading its furni passes itself in: it is not in the room
    /// manager yet, so the item cannot find it, and the tick is queued on the
    /// room's own handler rather than through RequestUpdate.
    /// </summary>
    public static void OnLoaded(Item item, Room? room = null, bool placed = false)
    {
        room ??= item.GetRoom();
        if (room == null || !IsSearchable(item, room))
            return;
        // OnPlace comes on a move too, for a bin already in the room and
        // already ticking: nothing about it has changed.
        if (placed && room.GetRoomItemHandler().GetItem(item.Id) != null)
            return;
        if (!IsSearched(item.Id))
        {
            if (item.LegacyDataString != "1")
                return;
            if (placed)
            {
                SetState(item, "0");
                return;
            }
            SearchedAt[item.Id] = Now;
        }
        if (item.LegacyDataString != "1")
            SetState(item, "1");
        item.RequestUpdate(1, false);
        room.GetRoomItemHandler().QueueRoomItemUpdate(item);
    }

    /// <summary>
    /// Ten seconds up: the find. Done whole under the Gate - the bin marked
    /// searched, the find written to the log and then paid, before anyone
    /// else can start on the bin or count the account's searches.
    /// </summary>
    private static void Finish(GameClient client, Item item, RoomUser user, Search search)
    {
        var habbo = client.GetHabbo();
        var room = item.GetRoom();
        string key;
        int amount;
        var given = 0;
        var stopped = false;

        lock (Gate)
        {
            // Stopped in the same moment: it ended that way instead.
            if (!ByUser.TryGetValue(search.UserId, out var current) || current != search)
                return;
            LastEnded[search.UserId] = Now;
            // What would have stopped it, asked once more at the moment of the
            // find: a trade opened since the tick looked freezes the backpack.
            if (RpTrades.IsTrading(search.UserId) || PoliceState.IsCuffed(search.UserId) || user.RpKnockedOut)
            {
                Release(search);
                stopped = true;
                key = BinRewards.Nothing;
                amount = 0;
            }
            else
            {
                SearchedAt[item.Id] = Now;
                Release(search);

                (key, amount) = BinRewards.Payout(BinRewards.Roll());
                // Its row first: nothing is paid that the daily count and the
                // log do not already hold.
                var logId = Log(search, key, amount);
                if (key == BinRewards.Coins)
                {
                    habbo.ChangeCredits(amount);
                    CoinLedger.Record(habbo, amount, "Searched a bin");
                    given = amount;
                }
                else if (key != BinRewards.Nothing)
                {
                    while (given < amount && habbo.AddRpItem(key) >= 0)
                        given++;
                }
                if (given != amount)
                    LogGiven(logId, given);
            }
        }

        RestoreEffect(user, search);
        if (stopped)
        {
            client.SendWhisper(StoppedMessage);
            return;
        }
        if (key == BinRewards.Nothing)
        {
            Say(room, user, "*finds nothing useful in the bin*");
        }
        else if (key == BinRewards.Coins)
        {
            client.Send(new CreditBalanceComposer(habbo.Credits));
            Say(room, user, $"*finds {amount}c in the bin*");
        }
        else
        {
            var name = BinRewards.NameOf(key);
            if (given > 0)
                client.Send(new RpInventoryComposer(habbo.LoadRpInventory()));
            Say(room, user, $"*finds {Describe(name, Math.Max(given, 1))} in the bin*");
            // A one-per-player item already held, or the backpack filled while
            // they searched: they saw it, but it stays in the bin.
            if (given < amount)
                client.SendWhisper(given == 0 ? $"You couldn't carry the {name}." : "Your backpack is full.");
        }

        SetState(item, "1");
        item.RequestUpdate((int)(ResetMs / CycleMs), false);
    }

    private static void Stop(int userId, bool tell)
    {
        Search? search;
        lock (Gate)
        {
            if (!ByUser.TryGetValue(userId, out search))
                return;
            Release(search);
            LastEnded[userId] = Now;
        }
        var client = PlusEnvironment.Game.ClientManager.GetClientByUserId(userId);
        var user = client?.GetHabbo()?.CurrentRoom?.GetRoomUserManager().GetRoomUserByHabbo(userId);
        if (user != null)
            RestoreEffect(user, search);
        if (tell)
            client?.SendWhisper(StoppedMessage);
    }

    // However it ends - found, stopped, or forgotten - it lets go of the bin
    // and the card goes. Only ever this search's own entries: a newer one by
    // the same player or on the same bin is left alone.
    private static void Release(Search search)
    {
        ByUser.TryRemove(new KeyValuePair<int, Search>(search.UserId, search));
        SearcherOf.TryRemove(new KeyValuePair<uint, int>(search.ItemId, search.UserId));
        PlusEnvironment.Game.ClientManager.GetClientByUserId(search.UserId)?.Send(new RpBinSearchComposer(false));
    }

    private static int FinishedToday(int accountId)
    {
        using var connection = PlusEnvironment.DatabaseManager.Connection();
        return connection.ExecuteScalar<int>(
            "SELECT COUNT(*) FROM `rp_bin_search_log` WHERE `account_id` = @accountId AND `created_at` > UNIX_TIMESTAMP() - @window",
            new { accountId, window = CapWindowSeconds });
    }

    // One row per finished search: what the bin gave, written as given in
    // full before it is paid. The row's id comes back for LogGiven.
    private static int Log(Search search, string reward, int amount)
    {
        using var connection = PlusEnvironment.DatabaseManager.Connection();
        return connection.QuerySingle<int>(
            "INSERT INTO `rp_bin_search_log` (`user_id`, `account_id`, `item_id`, `room_id`, `reward`, `amount`, `given`, `created_at`) " +
            "VALUES (@userId, @accountId, @itemId, @roomId, @reward, @amount, @amount, UNIX_TIMESTAMP()); SELECT LAST_INSERT_ID();",
            new { userId = search.UserId, accountId = search.AccountId, itemId = search.ItemId, roomId = search.RoomId, reward, amount });
    }

    // Less than all of it carried away: a full backpack, or a one-per-player
    // item already held.
    private static void LogGiven(int logId, int given)
    {
        using var connection = PlusEnvironment.DatabaseManager.Connection();
        connection.Execute("UPDATE `rp_bin_search_log` SET `given` = @given WHERE `id` = @logId LIMIT 1", new { logId, given });
    }

    // Only while the flies are still what is on: something else may have
    // taken the slot since, and that is not ours to undo.
    private static void RestoreEffect(RoomUser user, Search search)
    {
        if (user.GetClient()?.GetHabbo()?.Effects?.CurrentEffect == FliesEffectId)
            user.ApplyEffect(search.EffectBefore == FliesEffectId ? 0 : search.EffectBefore);
    }

    private static void SetState(Item item, string state)
    {
        item.LegacyDataString = state;
        item.UpdateState(true, true);
    }

    // The blue action bubble (style 4), wrapped in asterisks so the client
    // bolds it like every other RP action.
    private static void Say(Room? room, RoomUser user, string text) =>
        room?.SendPacket(new ChatComposer(user.VirtualId, text, 0, 4));

    private static string Describe(string name, int count)
    {
        if (count > 1)
            return name.Contains('(') || name.EndsWith('s') ? $"{count}x {name}" : $"{count} {name}s";
        return ("AEIOU".Contains(char.ToUpperInvariant(name.FirstOrDefault())) ? "an " : "a ") + name;
    }
}

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
/// Double-click it from beside it: ten seconds of rummaging, flies (effect 10)
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

    private static readonly object Gate = new();
    private static readonly ConcurrentDictionary<int, Search> ByUser = new();
    private static readonly ConcurrentDictionary<uint, int> SearcherOf = new();
    private static readonly ConcurrentDictionary<uint, long> SearchedAt = new();
    private static readonly ConcurrentDictionary<int, long> LastAttempt = new();

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

    /// <summary>A double-click on a searchable bin.</summary>
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
        // Like the vending machines: walk up to it first, then use it.
        if (!Gamemap.TilesTouching(user.X, user.Y, item.GetX, item.GetY))
        {
            user.MoveTo(item.SquareInFront);
            return;
        }
        // Started mid-step it would stop again on the next tile.
        if (user.IsWalking)
            return;
        user.SetRot(Rotation.Calculate(user.X, user.Y, item.GetX, item.GetY), false);

        // The answers that need nothing from the database first. All three are
        // asked again under the Gate below, which is where they count.
        if (ByUser.TryGetValue(habbo.Id, out var mine) && (now - mine.StartedAt) <= StaleMs)
        {
            if (mine.ItemId != item.Id)
                session.SendWhisper("You're already searching a bin.");
            return;
        }
        if (IsSearched(item.Id))
        {
            session.SendWhisper("This bin has already been searched.");
            return;
        }
        if (SearcherOf.TryGetValue(item.Id, out var busy) && busy != habbo.Id
            && ByUser.TryGetValue(busy, out var busyWith) && busyWith.ItemId == item.Id && (now - busyWith.StartedAt) <= StaleMs)
        {
            session.SendWhisper("Someone is already searching this bin.");
            return;
        }

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
        item.RequestUpdate(1, true);
    }

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

    /// <summary>A bin picked up: whoever was searching it has nothing left to search.</summary>
    public static void OnRemoved(Item item)
    {
        if (SearcherOf.TryGetValue(item.Id, out var userId))
            Stop(userId, true);
    }

    /// <summary>
    /// The bin's own update tick: a search in progress, or the fifteen minutes
    /// running out. Called from inside the room's update pass
    /// (Item.ProcessUpdates), so it re-arms with the counter alone - the pass
    /// keeps an item with a count left for the next cycle. Queueing it again
    /// from here would put it straight back into the pass that is running.
    /// </summary>
    public static void Tick(Item item)
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
                    habbo.Credits += amount;
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

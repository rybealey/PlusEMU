using System.Collections.Concurrent;
using Plus.Communication.Packets.Outgoing.Inventory.Purse;
using Plus.Communication.Packets.Outgoing.Rooms.Chat;
using Plus.Communication.Packets.Outgoing.Users;
using Plus.HabboHotel.CityPanel;
using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Items;
using Plus.HabboHotel.Rooms.Chat.Commands.User.Police;
using Plus.HabboHotel.Rooms.PathFinding;
using Plus.HabboHotel.Rooms.Trading;
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
/// Both clocks run on the bin's own update ticks (Item.ProcessUpdates): once a
/// cycle while someone searches, and once at the end of the fifteen minutes.
/// When a bin was searched lives here, by item id, rather than on the room, so
/// a room emptying and loading again does not shut its bins early; only a
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
    public const string StoppedMessage = "You stopped interacting with the item.";

    // A room cycle is half a second; the reset is re-checked against the
    // clock when it comes due, so a slow room only ever shuts a bin late.
    private const int CycleMs = 500;

    private sealed class Search
    {
        public int UserId;
        public uint ItemId;
        public uint RoomId;
        public int X;
        public int Y;
        public long StartedAt;
        public int EffectBefore;
    }

    private static readonly ConcurrentDictionary<int, Search> ByUser = new();
    private static readonly ConcurrentDictionary<uint, int> SearcherOf = new();
    private static readonly ConcurrentDictionary<uint, long> SearchedAt = new();

    private static long Now => Environment.TickCount64;

    public static bool IsSearchable(Item item, Room? room = null) =>
        item?.Definition?.InteractionType == InteractionType.BinSearch && PixelRpRooms.IsPixelRpRoom(room ?? item.GetRoom());

    private static bool IsSearched(uint itemId) =>
        SearchedAt.TryGetValue(itemId, out var at) && (Now - at) < ResetMs;

    /// <summary>A double-click on a searchable bin.</summary>
    public static void Start(GameClient session, Item item)
    {
        var habbo = session?.GetHabbo();
        var room = item.GetRoom();
        var user = room?.GetRoomUserManager().GetRoomUserByHabbo(habbo?.Id ?? 0);
        if (habbo == null || user == null)
            return;
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

        // A search whose bin stopped ticking - its room unloaded under it -
        // is long past its ten seconds; let it go quietly.
        if (ByUser.TryGetValue(habbo.Id, out var current) && (Now - current.StartedAt) > SearchMs + 5_000)
        {
            End(current);
            current = null;
        }
        if (current != null)
        {
            if (current.ItemId != item.Id)
                session.SendWhisper("You're already searching a bin.");
            return;
        }
        if (SearcherOf.TryGetValue(item.Id, out var searcher))
        {
            if (ByUser.TryGetValue(searcher, out var theirs) && (Now - theirs.StartedAt) <= SearchMs + 5_000)
            {
                session.SendWhisper("Someone is already searching this bin.");
                return;
            }
            SearcherOf.TryRemove(item.Id, out _);
        }
        if (IsSearched(item.Id))
        {
            session.SendWhisper("This bin has already been searched.");
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

        var search = new Search
        {
            UserId = habbo.Id, ItemId = item.Id, RoomId = room!.Id, X = user.X, Y = user.Y,
            StartedAt = Now, EffectBefore = habbo.Effects?.CurrentEffect ?? 0
        };
        if (!SearcherOf.TryAdd(item.Id, habbo.Id))
            return;
        ByUser[habbo.Id] = search;
        user.ApplyEffect(FliesEffectId);
        Say(room, user, "*starts searching through the bin*");
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

    /// <summary>The bin's own update tick: a search in progress, or the fifteen minutes running out.</summary>
    public static void Tick(Item item)
    {
        if (SearcherOf.TryGetValue(item.Id, out var userId))
        {
            if (!ByUser.TryGetValue(userId, out var search) || search.ItemId != item.Id)
            {
                SearcherOf.TryRemove(item.Id, out _);
                return;
            }
            var client = PlusEnvironment.Game.ClientManager.GetClientByUserId(userId);
            var habbo = client?.GetHabbo();
            var user = item.GetRoom()?.GetRoomUserManager().GetRoomUserByHabbo(userId);
            if (habbo == null || user == null || habbo.CurrentRoom?.Id != search.RoomId)
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
                Finish(client!, item, user, search);
                return;
            }
            item.RequestUpdate(1, true);
            return;
        }

        if (!SearchedAt.TryGetValue(item.Id, out var at))
            return;
        var left = ResetMs - (Now - at);
        if (left > 0)
        {
            item.RequestUpdate((int)(left / CycleMs) + 1, true);
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

    private static void Finish(GameClient client, Item item, RoomUser user, Search search)
    {
        End(search);
        RestoreEffect(user, search);

        var habbo = client.GetHabbo();
        var room = item.GetRoom();
        var reward = BinRewards.Roll();
        var amount = reward == null ? 0 : Random.Shared.Next(reward.Min, reward.Max + 1);

        if (reward == null || reward.Key == BinRewards.Nothing || amount <= 0)
        {
            Say(room, user, "*finds nothing useful in the bin*");
        }
        else if (reward.Key == BinRewards.Coins)
        {
            habbo.Credits += amount;
            CoinLedger.Record(habbo, amount, "Searched a bin");
            client.Send(new CreditBalanceComposer(habbo.Credits));
            Say(room, user, $"*finds {amount}c in the bin*");
        }
        else
        {
            var name = BinRewards.NameOf(reward.Key);
            var added = 0;
            while (added < amount && habbo.AddRpItem(reward.Key) >= 0)
                added++;
            if (added > 0)
                client.Send(new RpInventoryComposer(habbo.LoadRpInventory()));
            Say(room, user, $"*finds {Describe(name, Math.Max(added, 1))} in the bin*");
            // A one-per-player item already held, or the backpack filled while
            // they searched: they saw it, but it stays in the bin.
            if (added < amount)
                client.SendWhisper(added == 0 ? $"You couldn't carry the {name}." : "Your backpack is full.");
        }

        SearchedAt[item.Id] = Now;
        SetState(item, "1");
        item.RequestUpdate((int)(ResetMs / CycleMs), true);
    }

    private static void Stop(int userId, bool tell)
    {
        if (!ByUser.TryGetValue(userId, out var search))
            return;
        End(search);
        var client = PlusEnvironment.Game.ClientManager.GetClientByUserId(userId);
        var user = client?.GetHabbo()?.CurrentRoom?.GetRoomUserManager().GetRoomUserByHabbo(userId);
        if (user != null)
            RestoreEffect(user, search);
        if (tell)
            client?.SendWhisper(StoppedMessage);
    }

    private static void End(Search search)
    {
        ByUser.TryRemove(search.UserId, out _);
        SearcherOf.TryRemove(search.ItemId, out _);
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

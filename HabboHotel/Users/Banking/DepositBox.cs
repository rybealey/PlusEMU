using Plus.Communication.Packets.Outgoing.Users;
using Plus.HabboHotel.Corporations;
using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Items;
using Plus.HabboHotel.Rooms;
using Plus.HabboHotel.Rooms.Chat.Commands.User.Police;

namespace Plus.HabboHotel.Users.Banking;

/// <summary>
/// pixelrp: the bank deposit box (design: the "Bank Deposit Box" canvas).
///
/// Every player with a bank account has one - a second set of slots kept at
/// the bank (user_rp_deposit_box, migration 238). Stepping onto furni with the
/// `deposit_box` behaviour opens it beside the backpack, stepping off closes
/// it, and every move is checked against that: the box is only reachable
/// while standing on one. That is the point of it - what is stored is out of
/// reach until its owner comes back to the bank.
///
/// Moves go the backpack's own way. A click moves one, a drag moves the whole
/// stack; an item joins a stack of its kind with room (ten to a stack, weapons
/// never stack), else the first free slot; the handcuffs rule holds coming
/// out (RpInventoryStore.Add). A move that runs out of room stops part way and
/// says so - nothing is ever lost between the two.
///
/// Police equipment never goes in (PoliceUtility.PoliceGear): the window greys
/// it out on the backpack side, from the list the box is sent with.
/// </summary>
public static class DepositBox
{
    /// <summary>Open to everybody.</summary>
    public const int BaseSlots = 16;

    /// <summary>With VIP: one more row.</summary>
    public const int VipSlots = 20;

    public const int Store = 0;
    public const int Withdraw = 1;
    /// <summary>Within the box: move an item to another slot, swapping with what is there.</summary>
    public const int Rearrange = 2;

    public static int OpenSlots(Habbo habbo) => habbo.IsVip ? VipSlots : BaseSlots;

    public static List<(int Slot, string Item, int Count)> Load(int userId)
    {
        var list = new List<(int, string, int)>();
        using var dbClient = PlusEnvironment.DatabaseManager.GetQueryReactor();
        dbClient.SetQuery("SELECT `slot`, `item`, `count` FROM `user_rp_deposit_box` WHERE `user_id` = @id ORDER BY `slot`");
        dbClient.AddParameter("id", userId);
        var table = dbClient.GetTable();
        if (table != null)
            foreach (System.Data.DataRow row in table.Rows)
                list.Add((Convert.ToInt32(row["slot"]), Convert.ToString(row["item"]) ?? "", Convert.ToInt32(row["count"])));
        // pixelrp: an item an older rearrange left in slot 0, back in sight (SlotRows).
        var stranded = list.FindIndex(entry => entry.Item1 == 0);
        if (stranded >= 0)
        {
            var free = SlotRows.RecoverStranded("user_rp_deposit_box", userId, list.Select(entry => entry.Item1).ToList(), VipSlots);
            if (free > 0)
            {
                list[stranded] = (free, list[stranded].Item2, list[stranded].Item3);
                list.Sort((a, b) => a.Item1.CompareTo(b.Item1));
            }
        }
        return list;
    }

    /// <summary>Whether the player is standing on a deposit box right now.</summary>
    public static bool IsAtBox(Room? room, RoomUser? user)
    {
        if (room == null || user == null)
            return false;
        var items = room.GetGameMap()?.GetAllRoomItemForSquare(user.X, user.Y);
        return items != null && items.Any(item => item?.Definition?.InteractionType == InteractionType.DepositBox);
    }

    /// <summary>Stepped onto a deposit box: open it, or say why it will not.</summary>
    public static void Open(GameClient? client)
    {
        var habbo = client?.GetHabbo();
        if (habbo == null)
            return;
        if (!BankUtility.HasAccount(habbo.Id))
        {
            client!.SendWhisper("Open a bank account to get a deposit box.");
            return;
        }
        // The backpack goes too: the window shows both, and this is the one
        // moment it is certainly current.
        client!.Send(new RpInventoryComposer(habbo.LoadRpInventory()));
        client.Send(new RpDepositBoxComposer(true, OpenSlots(habbo), Load(habbo.Id)));
    }

    /// <summary>Stepped off: the window closes.</summary>
    public static void Close(GameClient? client)
    {
        if (client?.GetHabbo() == null)
            return;
        client.Send(new RpDepositBoxComposer(false, 0, new List<(int, string, int)>()));
    }

    /// <summary>
    /// <see cref="Open"/> for the step onto the furni. Stepping on fires on the
    /// movement thread, under the room's _cycleLock, and Open reads the backpack
    /// and the box from the database - so it runs on RoomTickWriter's thread
    /// instead. In order with <see cref="CloseSoon"/>, and only for somebody
    /// still standing on a box by then: a quick step on and off opens nothing.
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
            if (!IsAtBox(room, room?.GetRoomUserManager()?.GetRoomUserByHabbo(habbo.Id)))
                return;
            Open(client);
        });
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

    /// <summary>
    /// One move: store (backpack slot -> box) or withdraw (box slot -> backpack),
    /// one item or the whole stack. Returns what to tell the player.
    /// </summary>
    public static string Move(GameClient client, int direction, int slot, bool all, int toSlot = 0)
    {
        var habbo = client.GetHabbo();
        var room = habbo.CurrentRoom;
        var user = room?.GetRoomUserManager()?.GetRoomUserByHabbo(habbo.Id);
        if (!IsAtBox(room, user))
            return "Your deposit box is at the bank.";
        if (!BankUtility.HasAccount(habbo.Id))
            return "Open a bank account to get a deposit box.";
        if (PoliceState.IsCuffed(habbo.Id))
            return "Your hands are cuffed.";
        // pixelrp trading: the backpack is frozen while a trade is open.
        if (Rooms.Trading.RpTrades.IsTrading(habbo.Id))
            return Rooms.Trading.RpTrades.FrozenMessage;

        return direction switch
        {
            Store => StoreItems(habbo, slot, all),
            Withdraw => WithdrawItems(habbo, slot, all),
            Rearrange => RearrangeItems(habbo, slot, toSlot),
            _ => ""
        };
    }

    /// <summary>
    /// Move the item in one box slot to another: into it when it is empty, a
    /// swap when it is not. Only open slots are a destination - an item may sit
    /// in a VIP slot after the VIP ran out, and can be moved out of it, but
    /// nothing new goes in.
    /// </summary>
    private static string RearrangeItems(Habbo habbo, int from, int to)
    {
        if (from == to || to < 1 || to > OpenSlots(habbo))
            return "";
        if (Load(habbo.Id).All(row => row.Slot != from))
            return "";
        // The backpack's move, all or nothing (SlotRows).
        SlotRows.Move("user_rp_deposit_box", habbo.Id, from, to);
        return "";
    }

    private static string StoreItems(Habbo habbo, int slot, bool all)
    {
        // Only the carry slots: an equipped weapon is put away first.
        if (slot < 1 || slot > Habbo.RpCarrySlots)
            return "Put it in your backpack first.";
        var entry = habbo.LoadRpInventory().FirstOrDefault(row => row.Slot == slot);
        if (string.IsNullOrEmpty(entry.Item))
            return "";
        // Police equipment stays out (PoliceUtility.PoliceGear). The window
        // greys it out and will not send it, so this only turns away a client
        // that sends it anyway - and says nothing.
        if (PoliceUtility.IsPoliceGear(entry.Item))
            return "";
        var wanted = all ? entry.Count : 1;
        var moved = 0;
        var open = OpenSlots(habbo);
        // a weapon or armour is one to a slot, and goes in as worn as it is (RpDurability)
        var wear = RpInventoryStore.WearOf(habbo.Id, slot);
        while (moved < wanted)
        {
            if (AddOne(habbo.Id, entry.Item, open, wear) < 0)
                break;
            RpInventoryStore.Consume(habbo.Id, slot);
            moved++;
        }
        if (moved == 0)
            return "Your deposit box is full.";
        return moved < wanted ? $"Stored {moved} - your deposit box is full." : "";
    }

    private static string WithdrawItems(Habbo habbo, int slot, bool all)
    {
        var entry = Load(habbo.Id).FirstOrDefault(row => row.Slot == slot);
        if (string.IsNullOrEmpty(entry.Item))
            return "";
        var wanted = all ? entry.Count : 1;
        var moved = 0;
        var result = 0;
        var wear = WearOf(habbo.Id, slot);
        while (moved < wanted)
        {
            result = RpInventoryStore.Add(habbo.Id, entry.Item, habbo.RpUnlockedSlots, wear);
            if (result < 0)
                break;
            RemoveOne(habbo.Id, slot);
            moved++;
        }
        if (moved == wanted)
            return "";
        var why = result == Habbo.RpAlreadyHeld ? "you can only carry one" : "your backpack is full";
        return moved == 0 ? char.ToUpper(why[0]) + why[1..] + "." : $"Took out {moved} - {why}.";
    }

    /// <summary>pixelrp: how worn the weapon or armour in one box slot is (RpDurability); 0 for anything else.</summary>
    private static int WearOf(int userId, int slot)
    {
        using var dbClient = PlusEnvironment.DatabaseManager.GetQueryReactor();
        dbClient.SetQuery("SELECT `wear` FROM `user_rp_deposit_box` WHERE `user_id` = @id AND `slot` = @slot LIMIT 1");
        dbClient.AddParameter("id", userId);
        dbClient.AddParameter("slot", slot);
        return dbClient.GetInteger();
    }

    /// <summary>
    /// One of an item into the box: onto a stack of it with room, else the
    /// first free open slot. The slot, or -1 when the box is full. A weapon or
    /// armour takes its wear with it (RpDurability).
    /// </summary>
    private static int AddOne(int userId, string item, int openSlots, int wear = 0)
    {
        var box = Load(userId);
        var existing = RpDurability.NeverStacks(item)
            ? default
            : box.FirstOrDefault(row => row.Item == item && row.Count < Habbo.RpStackCap && row.Slot <= openSlots);
        using var dbClient = PlusEnvironment.DatabaseManager.GetQueryReactor();
        if (existing.Item == item && existing.Slot > 0)
        {
            dbClient.SetQuery("UPDATE `user_rp_deposit_box` SET `count` = `count` + 1 WHERE `user_id` = @id AND `slot` = @slot");
            dbClient.AddParameter("id", userId);
            dbClient.AddParameter("slot", existing.Slot);
            dbClient.RunQuery();
            return existing.Slot;
        }
        var used = box.Select(row => row.Slot).ToHashSet();
        var slot = Enumerable.Range(1, openSlots).FirstOrDefault(candidate => !used.Contains(candidate));
        if (slot == 0)
            return -1;
        dbClient.SetQuery("INSERT INTO `user_rp_deposit_box` (`user_id`, `slot`, `item`, `count`, `wear`) VALUES (@id, @slot, @item, 1, @wear)");
        dbClient.AddParameter("id", userId);
        dbClient.AddParameter("slot", slot);
        dbClient.AddParameter("item", item);
        dbClient.AddParameter("wear", RpDurability.IsDurable(item) ? Math.Max(0, wear) : 0);
        dbClient.RunQuery();
        return slot;
    }

    private static void RemoveOne(int userId, int slot)
    {
        using var dbClient = PlusEnvironment.DatabaseManager.GetQueryReactor();
        dbClient.SetQuery("UPDATE `user_rp_deposit_box` SET `count` = `count` - 1 WHERE `user_id` = @id AND `slot` = @slot AND `count` > 0");
        dbClient.AddParameter("id", userId);
        dbClient.AddParameter("slot", slot);
        dbClient.RunQuery();
        dbClient.SetQuery("DELETE FROM `user_rp_deposit_box` WHERE `user_id` = @id AND `slot` = @slot AND `count` <= 0");
        dbClient.AddParameter("id", userId);
        dbClient.AddParameter("slot", slot);
        dbClient.RunQuery();
    }
}

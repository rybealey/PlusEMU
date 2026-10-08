using Plus.Communication.Packets.Outgoing.Users;
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
/// </summary>
public static class DepositBox
{
    /// <summary>Open to everybody.</summary>
    public const int BaseSlots = 16;

    /// <summary>With VIP: one more row.</summary>
    public const int VipSlots = 20;

    public const int Store = 0;
    public const int Withdraw = 1;

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
    /// One move: store (backpack slot -> box) or withdraw (box slot -> backpack),
    /// one item or the whole stack. Returns what to tell the player.
    /// </summary>
    public static string Move(GameClient client, int direction, int slot, bool all)
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

        return direction == Store ? StoreItems(habbo, slot, all) : WithdrawItems(habbo, slot, all);
    }

    private static string StoreItems(Habbo habbo, int slot, bool all)
    {
        // Only the carry slots: an equipped weapon is put away first.
        if (slot < 1 || slot > Habbo.RpCarrySlots)
            return "Put it in your backpack first.";
        var entry = habbo.LoadRpInventory().FirstOrDefault(row => row.Slot == slot);
        if (string.IsNullOrEmpty(entry.Item))
            return "";
        var wanted = all ? entry.Count : 1;
        var moved = 0;
        var open = OpenSlots(habbo);
        while (moved < wanted)
        {
            if (AddOne(habbo.Id, entry.Item, open) < 0)
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
        while (moved < wanted)
        {
            result = RpInventoryStore.Add(habbo.Id, entry.Item, habbo.RpUnlockedSlots);
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

    /// <summary>
    /// One of an item into the box: onto a stack of it with room, else the
    /// first free open slot. The slot, or -1 when the box is full.
    /// </summary>
    private static int AddOne(int userId, string item, int openSlots)
    {
        var box = Load(userId);
        var existing = RpWeapons.IsWeapon(item)
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
        dbClient.SetQuery("INSERT INTO `user_rp_deposit_box` (`user_id`, `slot`, `item`, `count`) VALUES (@id, @slot, @item, 1)");
        dbClient.AddParameter("id", userId);
        dbClient.AddParameter("slot", slot);
        dbClient.AddParameter("item", item);
        dbClient.RunQuery();
        return slot;
    }

    private static void RemoveOne(int userId, int slot)
    {
        using var dbClient = PlusEnvironment.DatabaseManager.GetQueryReactor();
        dbClient.SetQuery("UPDATE `user_rp_deposit_box` SET `count` = `count` - 1 WHERE `user_id` = @id AND `slot` = @slot");
        dbClient.AddParameter("id", userId);
        dbClient.AddParameter("slot", slot);
        dbClient.RunQuery();
        dbClient.SetQuery("DELETE FROM `user_rp_deposit_box` WHERE `user_id` = @id AND `slot` = @slot AND `count` <= 0");
        dbClient.AddParameter("id", userId);
        dbClient.AddParameter("slot", slot);
        dbClient.RunQuery();
    }
}

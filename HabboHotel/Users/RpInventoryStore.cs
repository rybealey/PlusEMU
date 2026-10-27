using Dapper;
using Plus.Core;
using Plus.HabboHotel.Rooms.Chat.Commands.User.Police;

namespace Plus.HabboHotel.Users;

/// <summary>
/// pixelrp RP inventory (the backpack), by user id.
///
/// The backpack was only ever reachable through a logged-in Habbo, and every
/// one of its methods only ever used the Habbo's id - plus IsVip, for how many
/// slots are open. So the logic lives here keyed by (user id, open slots), and
/// the Habbo methods call it: the City Panel can read and edit an OFFLINE
/// player's backpack through exactly the same rules a logged-in one gets.
///
/// No caching - reads and writes go straight to user_rp_inventory, as before.
/// Whoever changes a logged-in player's backpack sends them RpInventoryComposer.
/// </summary>
public static class RpInventoryStore
{
    /// <summary>
    /// Items nobody may hold more than one of. Handcuffs: an officer carries
    /// one pair - the locker hands out a new pair when theirs is on a suspect
    /// or lost, and a pair coming back off a suspect to someone who already
    /// has one is simply lost. The Cop Medkit: one to an officer, and the
    /// locker hands out another once it is used. Enforced here because every
    /// way into a backpack goes through <see cref="Add"/>.
    /// </summary>
    private static readonly HashSet<string> OnePerPlayer = new() { CuffCommand.HandcuffsItem, Corporations.PoliceUtility.CopMedkitItem };

    public static bool IsOnePerPlayer(string item) => OnePerPlayer.Contains(item);

    public static List<(int Slot, string Item, int Count)> Load(int userId)
    {
        var list = new RpInventoryList();
        using var dbClient = PlusEnvironment.DatabaseManager.GetQueryReactor();
        dbClient.SetQuery("SELECT `slot`,`item`,`count`,`wear` FROM `user_rp_inventory` WHERE `user_id` = @id ORDER BY `slot`");
        dbClient.AddParameter("id", userId);
        var table = dbClient.GetTable();
        if (table != null)
            foreach (System.Data.DataRow row in table.Rows)
            {
                var slot = Convert.ToInt32(row["slot"]);
                list.Add((slot, Convert.ToString(row["item"]), Convert.ToInt32(row["count"])));
                list.Wear[slot] = Convert.ToInt32(row["wear"]);
            }
        // pixelrp: an item an older move left in slot 0, back in sight (SlotRows).
        var stranded = list.FindIndex(entry => entry.Slot == 0);
        if (stranded >= 0)
        {
            var free = SlotRows.RecoverStranded("user_rp_inventory", userId, list.Select(entry => entry.Slot).ToList(), Habbo.RpCarrySlots);
            if (free > 0)
            {
                list[stranded] = (free, list[stranded].Item, list[stranded].Count);
                if (list.Wear.Remove(0, out var wear))
                    list.Wear[free] = wear;
                list.Sort((a, b) => a.Slot.CompareTo(b.Slot));
            }
        }
        return list;
    }

    /// <summary>pixelrp: how worn the item in one slot is (RpDurability) - 0 for an empty slot or an item that does not wear.</summary>
    public static int WearOf(int userId, int slot)
    {
        using var dbClient = PlusEnvironment.DatabaseManager.GetQueryReactor();
        dbClient.SetQuery("SELECT `wear` FROM `user_rp_inventory` WHERE `user_id` = @id AND `slot` = @slot LIMIT 1");
        dbClient.AddParameter("id", userId);
        dbClient.AddParameter("slot", slot);
        return dbClient.GetInteger();
    }

    /// <summary>
    /// Adds one of an item (stacking onto an existing slot of the same item,
    /// else the first free carry slot). Returns the slot, -1 when the backpack
    /// is full, <see cref="Habbo.RpAlreadyHeld"/> for a one-per-player item
    /// already held, or <see cref="Habbo.RpAddFailed"/> when the write did not
    /// land. <paramref name="wear"/> is how worn it already is - a weapon or
    /// armour coming out of the deposit box keeps its wear (RpDurability);
    /// anything new starts at 0. <paramref name="fillVape"/> false is for a
    /// vape coming back to the player it left (the deposit box, a Marketplace
    /// listing taken back): it is the same vape, so it keeps their hits.
    /// </summary>
    public static int Add(int userId, string item, int unlockedSlots, int wear = 0, bool fillVape = true)
    {
        var inventory = Load(userId);
        if (OnePerPlayer.Contains(item) && inventory.Any(entry => entry.Item == item))
            return Habbo.RpAlreadyHeld;
        // pixelrp: weapons and armour never stack - each is its own item, one
        // to a slot, with its own wear - and only the carry slots are stacked
        // onto, never the Weapon frame.
        var existing = RpDurability.NeverStacks(item)
            ? default
            : inventory.FirstOrDefault(entry => entry.Item == item && entry.Count < Habbo.RpStackCap && entry.Slot >= 1 && entry.Slot <= Habbo.RpCarrySlots);
        // pixelrp: through Dapper, so a write that fails says so. The query
        // reactor only logs (QueryAdapter.RunQuery), and Add answered with a
        // slot for an item that never arrived - the Marketplace took a buyer's
        // cash for it, and a take-back ended the listing and lost its items.
        try
        {
            using var connection = PlusEnvironment.DatabaseManager.Connection();
            if (existing.Item == item && existing.Slot > 0 && existing.Slot <= unlockedSlots)
            {
                // Only onto that stack as it was read: moved, used up or full
                // by now, and nothing lands on whatever is there instead.
                var stacked = connection.Execute(
                    "UPDATE `user_rp_inventory` SET `count` = `count` + 1 WHERE `user_id` = @id AND `slot` = @slot AND `item` = @item AND `count` < @cap",
                    new { id = userId, slot = existing.Slot, item, cap = Habbo.RpStackCap });
                return stacked == 1 ? existing.Slot : Habbo.RpAddFailed;
            }
            var used = inventory.Select(entry => entry.Slot).ToHashSet();
            var slot = Enumerable.Range(1, unlockedSlots).FirstOrDefault(candidate => !used.Contains(candidate));
            if (slot == 0)
                return -1;
            // The slot filled in between: the (user_id, slot) key refuses it and that throws.
            connection.Execute(
                "INSERT INTO `user_rp_inventory` (`user_id`,`slot`,`item`,`count`,`wear`) VALUES (@id,@slot,@item,1,@wear)",
                new { id = userId, slot, item, wear = RpDurability.IsDurable(item) ? Math.Max(0, wear) : 0 });
            // pixelrp: hits are counted per player (RpVape), so a vape coming
            // into a backpack that holds none starts full. It goes into the
            // hand only once it is equipped (RpWeapons.IsEquippable).
            if (fillVape && item == RpVape.Item && !inventory.Any(entry => entry.Item == item))
                RpVape.Refill(userId);
            return slot;
        }
        catch (Exception e)
        {
            ExceptionLogger.LogException(e);
            return Habbo.RpAddFailed;
        }
    }

    /// <summary>
    /// Put a new weapon straight into the Weapon slot, which must be empty.
    /// False when it is not. Needs no free carry slot.
    /// </summary>
    public static bool AddEquipped(int userId, string item)
    {
        if (!RpWeapons.IsWeapon(item) || !string.IsNullOrEmpty(RpWeapons.EquippedItem(Load(userId))))
            return false;
        using var dbClient = PlusEnvironment.DatabaseManager.GetQueryReactor();
        dbClient.SetQuery("INSERT INTO `user_rp_inventory` (`user_id`,`slot`,`item`,`count`) VALUES (@id,@slot,@item,1)");
        dbClient.AddParameter("id", userId);
        dbClient.AddParameter("slot", RpWeapons.WeaponSlot);
        dbClient.AddParameter("item", item);
        dbClient.RunQuery();
        return true;
    }

    /// <summary>
    /// Moves the item in `from` into `to`, swapping when the target slot is
    /// occupied - all or nothing (SlotRows).
    /// </summary>
    public static void Move(int userId, int from, int to) => SlotRows.Move("user_rp_inventory", userId, from, to);

    /// <summary>Removes one of whatever sits in the slot. The item key, or null when it is empty.</summary>
    public static string Consume(int userId, int slot)
    {
        var entry = Load(userId).FirstOrDefault(candidate => candidate.Slot == slot);
        if (string.IsNullOrEmpty(entry.Item))
            return null;
        using var dbClient = PlusEnvironment.DatabaseManager.GetQueryReactor();
        if (entry.Count > 1)
            dbClient.SetQuery("UPDATE `user_rp_inventory` SET `count` = `count` - 1 WHERE `user_id` = @id AND `slot` = @slot");
        else
            dbClient.SetQuery("DELETE FROM `user_rp_inventory` WHERE `user_id` = @id AND `slot` = @slot");
        dbClient.AddParameter("id", userId);
        dbClient.AddParameter("slot", slot);
        dbClient.RunQuery();
        if (entry.Item == RpVape.Item)
            RpVape.SyncHand(userId);       // the last equipped one gone empties the hand
        return entry.Item;
    }

    /// <summary>
    /// Throws away `count` of what sits in the slot, the whole stack when
    /// `count` covers it. The item key and how many went, or (null, 0).
    /// </summary>
    public static (string Item, int Count) Discard(int userId, int slot, int count)
    {
        var entry = Load(userId).FirstOrDefault(candidate => candidate.Slot == slot);
        if (string.IsNullOrEmpty(entry.Item) || count < 1)
            return (null, 0);
        var removed = Math.Min(count, entry.Count);
        using var dbClient = PlusEnvironment.DatabaseManager.GetQueryReactor();
        if (removed < entry.Count)
        {
            dbClient.SetQuery("UPDATE `user_rp_inventory` SET `count` = `count` - @removed WHERE `user_id` = @id AND `slot` = @slot");
            dbClient.AddParameter("removed", removed);
        }
        else
            dbClient.SetQuery("DELETE FROM `user_rp_inventory` WHERE `user_id` = @id AND `slot` = @slot");
        dbClient.AddParameter("id", userId);
        dbClient.AddParameter("slot", slot);
        dbClient.RunQuery();
        if (entry.Item == RpVape.Item)
            RpVape.SyncHand(userId);
        return (entry.Item, removed);
    }

    /// <summary>
    /// City Panel: set a stack to an exact count. 0 empties the slot. A weapon
    /// only ever holds one, and nothing goes past the stack cap. False when the
    /// slot is empty.
    /// </summary>
    public static bool SetCount(int userId, int slot, int count)
    {
        var entry = Load(userId).FirstOrDefault(candidate => candidate.Slot == slot);
        if (string.IsNullOrEmpty(entry.Item))
            return false;
        var cap = (RpDurability.NeverStacks(entry.Item) || OnePerPlayer.Contains(entry.Item)) ? 1 : Habbo.RpStackCap;
        var target = Math.Clamp(count, 0, cap);
        using var dbClient = PlusEnvironment.DatabaseManager.GetQueryReactor();
        if (target == 0)
            dbClient.SetQuery("DELETE FROM `user_rp_inventory` WHERE `user_id` = @id AND `slot` = @slot");
        else
        {
            dbClient.SetQuery("UPDATE `user_rp_inventory` SET `count` = @count WHERE `user_id` = @id AND `slot` = @slot");
            dbClient.AddParameter("count", target);
        }
        dbClient.AddParameter("id", userId);
        dbClient.AddParameter("slot", slot);
        dbClient.RunQuery();
        if (entry.Item == RpVape.Item)
            RpVape.SyncHand(userId);
        return true;
    }
}

/// <summary>
/// pixelrp: a backpack as <see cref="RpInventoryStore.Load"/> reads it - the
/// rows, plus how worn each one is, by slot (RpDurability). Still the plain
/// list of rows to everything that only wants those; the backpack packet
/// (RpInventoryComposer) also sends the wear, so a bar can show it.
/// </summary>
public class RpInventoryList : List<(int Slot, string Item, int Count)>
{
    public Dictionary<int, int> Wear { get; } = new();
}

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
        var list = new List<(int, string, int)>();
        using var dbClient = PlusEnvironment.DatabaseManager.GetQueryReactor();
        dbClient.SetQuery("SELECT `slot`,`item`,`count` FROM `user_rp_inventory` WHERE `user_id` = @id ORDER BY `slot`");
        dbClient.AddParameter("id", userId);
        var table = dbClient.GetTable();
        if (table != null)
            foreach (System.Data.DataRow row in table.Rows)
                list.Add((Convert.ToInt32(row["slot"]), Convert.ToString(row["item"]), Convert.ToInt32(row["count"])));
        return list;
    }

    /// <summary>
    /// Adds one of an item (stacking onto an existing slot of the same item,
    /// else the first free carry slot). Returns the slot, -1 when the backpack
    /// is full, or <see cref="Habbo.RpAlreadyHeld"/> for a one-per-player item
    /// already held.
    /// </summary>
    public static int Add(int userId, string item, int unlockedSlots)
    {
        var inventory = Load(userId);
        if (OnePerPlayer.Contains(item) && inventory.Any(entry => entry.Item == item))
            return Habbo.RpAlreadyHeld;
        // pixelrp: weapons never stack - each is its own item, one to a slot -
        // and only the carry slots are stacked onto, never the Weapon frame.
        var existing = RpWeapons.IsWeapon(item)
            ? default
            : inventory.FirstOrDefault(entry => entry.Item == item && entry.Count < Habbo.RpStackCap && entry.Slot >= 1 && entry.Slot <= Habbo.RpCarrySlots);
        using var dbClient = PlusEnvironment.DatabaseManager.GetQueryReactor();
        if (existing.Item == item && existing.Slot > 0 && existing.Slot <= unlockedSlots)
        {
            dbClient.SetQuery("UPDATE `user_rp_inventory` SET `count` = `count` + 1 WHERE `user_id` = @id AND `slot` = @slot");
            dbClient.AddParameter("id", userId);
            dbClient.AddParameter("slot", existing.Slot);
            dbClient.RunQuery();
            return existing.Slot;
        }
        var used = inventory.Select(entry => entry.Slot).ToHashSet();
        var slot = Enumerable.Range(1, unlockedSlots).FirstOrDefault(candidate => !used.Contains(candidate));
        if (slot == 0)
            return -1;
        dbClient.SetQuery("INSERT INTO `user_rp_inventory` (`user_id`,`slot`,`item`,`count`) VALUES (@id,@slot,@item,1)");
        dbClient.AddParameter("id", userId);
        dbClient.AddParameter("slot", slot);
        dbClient.AddParameter("item", item);
        dbClient.RunQuery();
        return slot;
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
    /// occupied. The three-step dance through temp slot 0 (never a real slot -
    /// they're 1-based) satisfies the (user_id, slot) primary key.
    /// </summary>
    public static void Move(int userId, int from, int to)
    {
        using var dbClient = PlusEnvironment.DatabaseManager.GetQueryReactor();
        dbClient.SetQuery("UPDATE `user_rp_inventory` SET `slot` = 0 WHERE `user_id` = @id AND `slot` = @from");
        dbClient.AddParameter("id", userId);
        dbClient.AddParameter("from", from);
        dbClient.RunQuery();
        dbClient.SetQuery("UPDATE `user_rp_inventory` SET `slot` = @from WHERE `user_id` = @id AND `slot` = @to");
        dbClient.AddParameter("id", userId);
        dbClient.AddParameter("from", from);
        dbClient.AddParameter("to", to);
        dbClient.RunQuery();
        dbClient.SetQuery("UPDATE `user_rp_inventory` SET `slot` = @to WHERE `user_id` = @id AND `slot` = 0");
        dbClient.AddParameter("id", userId);
        dbClient.AddParameter("to", to);
        dbClient.RunQuery();
    }

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
        var cap = (RpWeapons.IsWeapon(entry.Item) || OnePerPlayer.Contains(entry.Item)) ? 1 : Habbo.RpStackCap;
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
        return true;
    }
}

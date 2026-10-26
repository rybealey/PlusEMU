using Dapper;

namespace Plus.HabboHotel.Users;

/// <summary>
/// pixelrp: moving a row between slots of a table keyed by (user_id, slot) -
/// the backpack (user_rp_inventory) and the deposit box (user_rp_deposit_box).
///
/// A swap goes through slot 0 (never a real slot) for the key: three updates.
/// They were three separate queries, and a query that fails only logs
/// (QueryAdapter.RunQuery) - so one failing part way (the slot filled in
/// between by something else, a dropped connection) let the rest run anyway
/// and left the item in slot 0, out of sight, where the next move through
/// slot 0 tripped over it. Now the three go in one transaction, all or none,
/// and a row already stranded there the old way is put back on the next load
/// (RecoverStranded).
/// </summary>
internal static class SlotRows
{
    /// <summary>
    /// Move the row in `from` to `to`, swapping with whatever is there. On a
    /// failure nothing moves, and the snapshot the caller sends after shows
    /// the slots as they were.
    /// </summary>
    public static void Move(string table, int userId, int from, int to)
    {
        try
        {
            using var connection = PlusEnvironment.DatabaseManager.Connection();
            connection.Open();
            using var transaction = connection.BeginTransaction();
            var args = new { id = userId, fromSlot = from, toSlot = to };
            connection.Execute($"UPDATE `{table}` SET `slot` = 0 WHERE `user_id` = @id AND `slot` = @fromSlot", args, transaction);
            connection.Execute($"UPDATE `{table}` SET `slot` = @fromSlot WHERE `user_id` = @id AND `slot` = @toSlot", args, transaction);
            connection.Execute($"UPDATE `{table}` SET `slot` = @toSlot WHERE `user_id` = @id AND `slot` = 0", args, transaction);
            transaction.Commit();
        }
        catch (Exception e)
        {
            Core.ExceptionLogger.LogException(e);
        }
    }

    /// <summary>
    /// A row left in slot 0 by a move made before Move was one transaction, put
    /// in the first free slot up to `maxSlot`. Returns that slot, or 0 when
    /// there was nothing stranded or no room for it.
    /// </summary>
    public static int RecoverStranded(string table, int userId, ICollection<int> usedSlots, int maxSlot)
    {
        if (!usedSlots.Contains(0))
            return 0;
        var free = Enumerable.Range(1, maxSlot).FirstOrDefault(slot => !usedSlots.Contains(slot));
        if (free == 0)
            return 0;
        using var dbClient = PlusEnvironment.DatabaseManager.GetQueryReactor();
        dbClient.SetQuery($"UPDATE `{table}` SET `slot` = @free WHERE `user_id` = @id AND `slot` = 0");
        dbClient.AddParameter("free", free);
        dbClient.AddParameter("id", userId);
        dbClient.RunQuery();
        return free;
    }
}

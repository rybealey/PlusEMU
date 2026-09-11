using Dapper;
using Plus.Communication.Packets.Outgoing.Inventory.Furni;
using Plus.Database;
using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Users.Inventory.Furniture;

namespace Plus.Communication.Packets.Incoming.Inventory.Furni;

/// <summary>
/// pixelrp: the inventory's trash bin. Destroys furni the player owns and is
/// not using - permanently, with no refund and no way back.
///
/// The client sends explicit item ids rather than a furni type, because the
/// stack it is binning is a CLIENT-side grouping: nitro groups by type AND
/// stuff data, so two colours of the same sofa are two stacks the server has
/// no notion of. Re-deriving the group here would bin the wrong items. Ids
/// also mean every single one is checked against the sender's own inventory
/// before anything is destroyed.
///
/// GetItem is the whole ownership check. The furniture inventory holds only
/// unplaced furni - placing removes it, picking it up puts it back - so an id
/// that resolves proves both that the item is the sender's AND that it is not
/// currently standing in a room. A crafted packet naming someone else's item,
/// or one that is placed, resolves to null and is skipped in silence.
///
/// The one case ownership does not cover is a trade: a trade holds items that
/// are still in the offering player's inventory until it completes, so binning
/// one mid-trade would leave the other side looking at furni that no longer
/// exists. Trading players are refused outright.
/// </summary>
internal class RpDeleteInventoryFurniEvent : IPacketEvent
{
    // A stack larger than this is binned over several packets. The cap bounds
    // the work one message can ask for; it is not a limit on stack size.
    private const int MaximumItems = 1000;

    private readonly IDatabase _database;

    public RpDeleteInventoryFurniEvent(IDatabase database) => _database = database;

    public Task Parse(GameClient session, IIncomingPacket packet)
    {
        var habbo = session.GetHabbo();

        if (habbo == null)
            return Task.CompletedTask;

        var count = packet.ReadInt();

        if (count <= 0 || count > MaximumItems)
            return Task.CompletedTask;

        var requested = new List<uint>(count);

        // The count came off the wire, so it is a claim rather than a fact:
        // ReadInt slices a fixed four bytes and throws once the buffer is spent,
        // and a packet promising a thousand ids while carrying three would take
        // that exception mid-loop.
        for (var i = 0; i < count && packet.HasDataRemaining(); i++)
            requested.Add((uint)packet.ReadInt());

        // Mid-trade the items are still in the inventory but are no longer the
        // player's alone to destroy - see the class note.
        if (habbo.CurrentRoom != null)
        {
            var roomUser = habbo.CurrentRoom.GetRoomUserManager().GetRoomUserByHabbo(habbo.Id);

            if (roomUser != null && roomUser.TradeId != 0)
            {
                session.SendNotification("You cannot delete furni while you are trading.");
                return Task.CompletedTask;
            }
        }

        // Resolve every id against the sender's own inventory BEFORE anything is
        // destroyed. Everything past this point works from the server's own
        // records, never from the numbers on the wire.
        var doomed = new List<InventoryItem>(requested.Count);

        // Distinct because a repeated id would otherwise be logged twice and
        // removed twice - harmless, but it would make the log read as though two
        // separate items were destroyed.
        foreach (var itemId in requested.Distinct())
        {
            var item = habbo.Inventory.Furniture.GetItem(itemId);

            if (item != null)
                doomed.Add(item);
        }

        if (doomed.Count == 0)
            return Task.CompletedTask;

        var ids = doomed.Select(item => item.Id).ToList();

        using (var connection = _database.Connection())
        {
            // The log is written BEFORE anything is destroyed, and the order is
            // load-bearing: it means the log can never MISS a deletion, only
            // ever name one that did not finish. Logging last would invert
            // that - any failure between the two writes would erase the items
            // and then throw, leaving furni gone from the database but still
            // drawn in the player's inventory until they relog, with no record
            // of what happened. This way such a failure destroys nothing.
            //
            // (The deploy does apply `106_FurniTrashBin.sql` itself, tracked in
            // `_applied_sql_updates`, so a missing table is not the expected
            // case - but it is the cheap one to be safe about.)
            //
            // Denormalised on purpose: it has to still read after the item is
            // gone and after a furni is renamed.
            connection.Execute(
                "INSERT INTO `rp_furni_delete_log` (`item_id`, `definition_id`, `item_name`, `user_id`, `username`) " +
                "VALUES (@itemId, @definitionId, @itemName, @userId, @username)",
                doomed.Select(item => new
                {
                    itemId = item.Id,
                    definitionId = item.Definition.Id,
                    itemName = item.Definition.ItemName ?? string.Empty,
                    userId = habbo.Id,
                    username = habbo.Username ?? string.Empty
                }).ToList());

            // user_id is redundant after the inventory check and stays anyway:
            // it is the last guard standing if this is ever called from
            // somewhere that has not done that check.
            connection.Execute("DELETE FROM `items` WHERE `id` IN @ids AND `user_id` = @userId",
                new { ids, userId = habbo.Id });

            // Orphaned side rows for the same ids. `items` has no cascade, and a
            // recycled id would otherwise inherit a stranger's group plate.
            connection.Execute("DELETE FROM `items_groups` WHERE `id` IN @ids", new { ids });
        }

        foreach (var item in doomed)
        {
            habbo.Inventory.Furniture.RemoveItem(item.Id);
            session.Send(new FurniListRemoveComposer(item.Id));
        }

        session.Send(new FurniListUpdateComposer());

        return Task.CompletedTask;
    }
}

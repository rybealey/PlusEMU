using Plus.Communication.Packets.Outgoing.Rooms.Engine;
using Plus.Communication.Packets.Outgoing.Rooms.Furni;
using Plus.Database;
using Plus.HabboHotel.GameClients;

namespace Plus.Communication.Packets.Incoming.Rooms.Furni;

/// <summary>
/// The stack tool / builder Tools height slider - nitro-renderer sends it as
/// ITEM_STACK_HELPER, which the revision routes here.
///
/// This always applied the height; what it never did was REMEMBER it. `z` was
/// set on the live item and nothing was written, so the height survived until
/// the next thing that made the client redraw the item - a reload, a rotate, a
/// drag - and then snapped back to whatever the stack said.
///
/// So the height is now recorded as the builder's INTENT (Item.CustomHeight,
/// persisted to `items`.`custom_height`) as well as applied, and MoveObjectEvent
/// re-applies it through SetFloorItem's `height` parameter, the same road :bh
/// takes. -1 means "no height was chosen, stack me normally", which is what the
/// widget's -100 sends.
/// </summary>
internal class UpdateMagicTileEvent : IPacketEvent
{
    // The ceiling the Tools widget, :bh and RpFurniFunctionEvent all use, so no
    // two of them can disagree about what a legal height is.
    private const double MaximumHeight = 40;

    private readonly IDatabase _database;

    public UpdateMagicTileEvent(IDatabase database) => _database = database;

    public Task Parse(GameClient session, IIncomingPacket packet)
    {
        if (!session.GetHabbo().InRoom)
            return Task.CompletedTask;
        var room = session.GetHabbo().CurrentRoom;
        if (room == null)
            return Task.CompletedTask;
        if (!room.CheckRights(session, false, true) && !session.GetHabbo().Permissions.HasRight("room_item_use_any_stack_tile"))
            return Task.CompletedTask;
        var itemId = packet.ReadUInt();
        var decimalHeight = packet.ReadInt();
        var item = room.GetRoomItemHandler().GetItem(itemId);
        if (item == null)
            return Task.CompletedTask;

        // Hundredths on the wire; the widget sends -100 for "sit on whatever is
        // already there", which lands below zero and clears the stored height.
        var height = decimalHeight / 100.0;
        var custom = (height < 0) ? -1 : Math.Min(height, MaximumHeight);

        item.CustomHeight = custom;
        if (custom >= 0)
            item.GetZ = custom;

        using (var dbClient = _database.GetQueryReactor())
        {
            dbClient.SetQuery("UPDATE `items` SET `custom_height` = @height, `z` = @z WHERE `id` = @itemId LIMIT 1");
            dbClient.AddParameter("height", custom);
            dbClient.AddParameter("z", item.GetZ);
            dbClient.AddParameter("itemId", itemId);
            dbClient.RunQuery();
        }

        // Walkability and stacking are baked into the map at generation time, so
        // an item that just moved up or down needs the map rebuilt to match.
        room.GetGameMap().GenerateMaps();

        room.SendPacket(new ObjectUpdateComposer(item));
        room.SendPacket(new UpdateMagicTileComposer(itemId, decimalHeight));
        return Task.CompletedTask;
    }
}

using Plus.Communication.Packets.Outgoing.Rooms.Engine;
using Plus.Communication.Packets.Outgoing.Rooms.Furni;
using Plus.Database;
using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Rooms;

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

        // pixelrp: the height AND the stored intent behind it, before either
        // changes, so :undo can put both back. Restoring z alone would leave
        // custom_height pointing at the height being undone, and the next move
        // would quietly re-apply it.
        var builder = room.GetRoomUserManager().GetRoomUserByHabbo(session.GetHabbo().Id);

        if (builder != null)
            builder.LastFurniUndo = FurniUndoState.Capture(item);

        // Hundredths on the wire. The widgets send -100 for "sit on whatever is
        // already there", which clears the stored height - and has to MOVE the
        // piece there too: on its own it only forgot the height, so On top left
        // the piece exactly where it was.
        //
        // 0 is the Floor button, and the floor of a raised tile is not 0: an
        // absolute 0 sank the piece into it. 0 means the floor of the tile the
        // piece stands on.
        var height = decimalHeight / 100.0;
        if (height < 0)
        {
            item.CustomHeight = -1;
            item.GetZ = room.GetRoomItemHandler().RestingHeight(item);
        }
        else
        {
            item.CustomHeight = (decimalHeight == 0)
                ? room.GetGameMap().FloorHeightAt(item.GetX, item.GetY)
                : Math.Min(height, MaximumHeight);
            item.GetZ = item.CustomHeight;
        }

        using (var dbClient = _database.GetQueryReactor())
        {
            dbClient.SetQuery("UPDATE `items` SET `custom_height` = @height, `z` = @z WHERE `id` = @itemId LIMIT 1");
            dbClient.AddParameter("height", item.CustomHeight);
            dbClient.AddParameter("z", item.GetZ);
            dbClient.AddParameter("itemId", itemId);
            dbClient.RunQuery();
        }

        // Walkability and stacking are baked into the map at generation time, so
        // an item that just moved up or down needs the map rebuilt to match.
        room.GetGameMap().GenerateMaps();

        room.SendPacket(new ObjectUpdateComposer(item));
        // The height the piece ended up at, not the one asked for: -100 and the
        // floor are not heights, and a readout echoing them showed the wrong
        // number (or 1.00, the stack widget's reading of -100).
        room.SendPacket(new UpdateMagicTileComposer(itemId, (int)Math.Round(item.GetZ * 100)));
        return Task.CompletedTask;
    }
}

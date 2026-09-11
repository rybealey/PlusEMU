using Plus.Database;
using Plus.HabboHotel.GameClients;

namespace Plus.Communication.Packets.Incoming.Rooms.Furni;

/// <summary>
/// pixelrp: the height slider in the builder Tools panel.
///
/// nitro-renderer sends this as ITEM_STACK_HELPER (wire 3839) with the height
/// in hundredths. The revision file already routed that wire id to
/// UpdateMagicTileEvent; there was simply no handler class of that name, and
/// PacketManager binds handlers to ClientPacketHeader by CLASS NAME. So the
/// slider only ever moved the sprite on the builder's own screen - nothing was
/// stored, and the height snapped back the moment anything made the client
/// redraw: a reload, a rotate, a drag.
///
/// The name has to stay UpdateMagicTileEvent for that binding to work.
///
/// The height is kept on the ITEM rather than only in `z`, because `z` is
/// where the item ended up and not what anyone asked for. See Item.CustomHeight.
/// A move re-applies it through SetFloorItem's `height` parameter, the same
/// road `:bh` takes.
/// </summary>
internal class UpdateMagicTileEvent : IPacketEvent
{
    // The ceiling the Tools widget, :bh and RpFurniFunctionEvent all use, so
    // no two of them can disagree about what a legal height is.
    private const double MaximumHeight = 40;

    private readonly IDatabase _database;

    public UpdateMagicTileEvent(IDatabase database) => _database = database;

    public Task Parse(GameClient session, IIncomingPacket packet)
    {
        var itemId = packet.ReadInt();
        // Hundredths on the wire. The stock client sends -100 to mean "clear",
        // which lands below zero here and puts the item back on the stack.
        var height = packet.ReadInt() / 100d;

        var room = session.GetHabbo()?.CurrentRoom;
        if (room == null)
            return Task.CompletedTask;
        if (!room.CheckRights(session, true))
            return Task.CompletedTask;

        var item = room.GetRoomItemHandler().GetItem((uint)itemId);
        if (item == null || item.RoomId != room.Id)
            return Task.CompletedTask;

        var custom = (height < 0) ? -1 : Math.Min(height, MaximumHeight);
        if (Math.Abs(item.CustomHeight - custom) < 0.0001)
            return Task.CompletedTask;

        item.CustomHeight = custom;

        // Re-placed where it already stands, with the chosen height handed to
        // the same `height` parameter :bh uses. That reuses the stacking, the
        // map rebuild and the broadcast rather than repeating them here - and a
        // cleared height (-1) falls through to normal stacking, which drops the
        // item back onto whatever it is standing on.
        room.GetRoomItemHandler().SetFloorItem(session, item, item.GetX, item.GetY, item.Rotation,
            false, false, true, height: custom);

        // Only the intent needs writing here: x/y/z ride the room's own save
        // cycle, which runs against whatever SetFloorItem settled on.
        using (var dbClient = _database.GetQueryReactor())
        {
            dbClient.SetQuery("UPDATE `items` SET `custom_height` = @height WHERE `id` = @itemId LIMIT 1");
            dbClient.AddParameter("height", custom);
            dbClient.AddParameter("itemId", itemId);
            dbClient.RunQuery();
        }

        return Task.CompletedTask;
    }
}

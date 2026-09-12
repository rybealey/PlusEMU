using Plus.Communication.Packets.Outgoing.Rooms.Engine;
using Plus.Communication.Packets.Outgoing.Rooms.Furni;
using Plus.Database;
using Plus.HabboHotel.GameClients;

namespace Plus.HabboHotel.Rooms.Chat.Commands.User;

/// <summary>
/// pixelrp: `:undo` puts the last piece of furni you moved, rotated,
/// re-levelled or faded back the way it was.
///
/// One step only, and only placement: this cannot bring back furni that was
/// picked up, traded or deleted. The trash bin in particular erases the item
/// row outright, so there is nothing left for an undo to restore - a builder
/// reaching for :undo after binning something has to be told that plainly
/// rather than left thinking it worked.
///
/// It forces. If somebody else has moved the piece since, their change is
/// overwritten; that was the deliberate choice over refusing, on the grounds
/// that most building is solo and a refusal nobody expected is worse than a
/// move they can redo by hand.
///
/// The undo itself is NOT recorded. Snapshots are only ever taken in the four
/// packet handlers a builder's actions come through, and this command restores
/// directly - so a second :undo reports there is nothing left instead of
/// bouncing the same piece back and forth forever.
/// </summary>
internal class UndoCommand : IChatCommand
{
    private readonly IDatabase _database;

    public UndoCommand(IDatabase database) => _database = database;

    public string Key => "undo";
    public string PermissionRequired => "command_undo";

    public string Parameters => "";

    public string Description => "Puts the last furniture you moved, rotated, raised or faded back the way it was.";

    public void Execute(GameClient session, Room room, string[] parameters)
    {
        var user = room.GetRoomUserManager().GetRoomUserByHabbo(session.GetHabbo().Id);

        if (user == null)
            return;

        var state = user.LastFurniUndo;

        if (state == null)
        {
            session.SendWhisper("There is nothing to undo. Move, rotate, raise or fade some furni first.");
            return;
        }

        if (!room.CheckRights(session))
        {
            session.SendWhisper("You need rights in this room to undo that.");
            return;
        }

        var item = room.GetRoomItemHandler().GetItem(state.ItemId);

        // Picked up, traded, sold or deleted since. Nothing here can bring any of
        // those back, so drop the snapshot rather than leave it to fail again.
        if (item == null)
        {
            user.LastFurniUndo = null;
            session.SendWhisper("That furni is no longer in this room, so it cannot be put back.");
            return;
        }

        if (state.IsWallItem)
        {
            item.WallCoordinates = state.WallCoordinates;
            room.GetRoomItemHandler().UpdateItem(item);
            room.SendPacket(new ItemUpdateComposer(item));
        }
        else
        {
            // The saved height is passed explicitly. Going through the normal
            // placement path would pick up the builder's CURRENT :bh instead, and
            // undo would put the piece back on the right tile at the wrong height.
            if (!room.GetRoomItemHandler().SetFloorItem(session, item, state.X, state.Y, state.Rotation, false, false, true,
                    height: state.Z))
            {
                // A genuine obstruction, not a stale snapshot: something else is on
                // that tile now, or someone is standing there. Keep the snapshot so
                // it can be retried once the way is clear.
                room.SendPacket(new ObjectUpdateComposer(item));
                session.SendWhisper("That spot is taken now, so the furni could not go back. Clear it and try again.");
                return;
            }
        }

        RestoreAlpha(room, item, state.Alpha);

        user.LastFurniUndo = null;
        session.SendWhisper("Undone. There is only one step, so that is as far back as it goes.");
    }

    /// <summary>
    /// Opacity is stored on the row rather than recalculated on load, so putting
    /// it back means writing it. Skipped when it has not changed, which is the
    /// usual case - the snapshot carries every placement field, not only the one
    /// the builder touched.
    /// </summary>
    private void RestoreAlpha(Room room, Items.Item item, int alpha)
    {
        if (item.Alpha == alpha)
            return;

        item.Alpha = alpha;

        using (var dbClient = _database.GetQueryReactor())
        {
            dbClient.SetQuery("UPDATE `items` SET `alpha` = @alpha WHERE `id` = @itemId LIMIT 1");
            dbClient.AddParameter("alpha", alpha);
            dbClient.AddParameter("itemId", item.Id);
            dbClient.RunQuery();
        }

        room.SendPacket(new RpFurniAlphaComposer(item));
    }
}

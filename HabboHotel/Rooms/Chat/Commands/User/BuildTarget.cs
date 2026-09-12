using System.Drawing;
using Plus.HabboHotel.Items;

namespace Plus.HabboHotel.Rooms.Chat.Commands.User;

/// <summary>
/// pixelrp: what a build command acts on.
///
/// The build commands are typed, not clicked, so they need a rule for "which
/// piece" that a builder can hold in their head. The rule is: the tile you are
/// FACING, and if there is nothing there, the tile you are standing on.
///
/// Facing first, because it is the half of the rule you can aim - you can
/// always turn to point at something, but you cannot always stand on it (a
/// plant, a wall, a piece somebody else is already on). Standing second,
/// because it is the only way to reach the piece under your own feet.
///
/// Every command whispers back what it touched, which is what makes an
/// occasional wrong guess cheap: you read the name, you step, you type it
/// again.
/// </summary>
internal static class BuildTarget
{
    private static IEnumerable<Point> Candidates(RoomUser actor)
    {
        var infront = actor.SquareInFront;

        // SquareInFront returns the actor's own tile on the diagonal facings,
        // so this is not always two distinct squares - the check below keeps
        // "facing, then standing" honest either way.
        yield return infront;

        if (infront.X != actor.X || infront.Y != actor.Y)
            yield return new Point(actor.X, actor.Y);
    }

    /// <summary>
    /// A bot on this tile. Pets are left out: they walk off on their own, so a
    /// facing you set for one would not survive the next minute.
    /// </summary>
    private static RoomUser BotOn(Room room, Point tile) =>
        room.GetGameMap().GetRoomUsers(tile).FirstOrDefault(x => x != null && x.IsBot && !x.IsPet && x.BotData != null);

    /// <summary>
    /// The topmost floor item on this tile - the piece a builder can actually
    /// see from where they stand, rather than the rug two items under it.
    /// </summary>
    private static Item FurniOn(Room room, Point tile) =>
        room.GetRoomItemHandler().GetFurniObjects(tile.X, tile.Y)
            .Where(x => x != null && x.IsFloorItem)
            .OrderByDescending(x => x.GetZ)
            .FirstOrDefault();

    /// <summary>
    /// For commands that can act on either. A bot wins the tile it shares with
    /// furniture - it is standing ON the piece it is posed with, so the piece
    /// can still be reached by stepping round it, while the bot can be reached
    /// no other way at all. Resolved one tile at a time, so what you are facing
    /// always beats what is under you.
    /// </summary>
    public static void Find(Room room, RoomUser actor, out RoomUser bot, out Item item)
    {
        foreach (var tile in Candidates(actor))
        {
            var onTile = BotOn(room, tile);

            if (onTile != null)
            {
                bot = onTile;
                item = null;
                return;
            }

            var furni = FurniOn(room, tile);

            if (furni != null)
            {
                bot = null;
                item = furni;
                return;
            }
        }

        bot = null;
        item = null;
    }

    /// <summary>
    /// For commands that only make sense on furniture. Bots are ignored rather
    /// than blocking, so a switch with somebody's shopkeeper standing on it can
    /// still be set.
    /// </summary>
    public static bool FindFurni(Room room, RoomUser actor, out Item item)
    {
        foreach (var tile in Candidates(actor))
        {
            item = FurniOn(room, tile);

            if (item != null)
                return true;
        }

        item = null;
        return false;
    }
}

using Plus.HabboHotel.Items;

namespace Plus.HabboHotel.Rooms;

/// <summary>
/// pixelrp: one furni's placement, captured just before a builder changed it,
/// so `:undo` can put it back.
///
/// Everything that describes where a piece sits is here - tile, height,
/// rotation, wall position and opacity - rather than only the field the action
/// touched. One shape means one restore path, and because only the single most
/// recent action is kept, this snapshot is always the state immediately before
/// it: restoring all of it cannot resurrect anything older than the change
/// being undone.
///
/// CustomHeight is carried alongside Z because the two say different things.
/// Z is where the piece ended up; CustomHeight is the height the builder CHOSE,
/// and it outlives any single move - MoveObjectEvent re-applies it on every
/// drag. Putting Z back without it would leave the stored intent pointing at
/// the height being undone, and the next drag would quietly restore it.
///
/// Deliberately NOT captured: anything that is not placement. Deleting furni is
/// permanent and there is nothing here that could bring it back.
/// </summary>
public sealed record FurniUndoState(
    uint ItemId,
    bool IsWallItem,
    int X,
    int Y,
    double Z,
    int Rotation,
    double CustomHeight,
    string WallCoordinates,
    int Alpha)
{
    public static FurniUndoState Capture(Item item) => new(
        item.Id,
        item.IsWallItem,
        item.GetX,
        item.GetY,
        item.GetZ,
        item.Rotation,
        item.CustomHeight,
        item.WallCoordinates,
        item.Alpha);
}

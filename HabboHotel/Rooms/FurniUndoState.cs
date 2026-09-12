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
        item.WallCoordinates,
        item.Alpha);
}

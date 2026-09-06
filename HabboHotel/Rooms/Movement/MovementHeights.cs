using Plus.HabboHotel.Rooms;

namespace Plus.HabboHotel.Rooms.Movement;

/// <summary>
/// pixelrp Movement V2: THE height authority for edge geometry.
///
/// There were two, and they disagreed. V2 edge Z came from the two-argument
/// Gamemap.SqAbsoluteHeight(x, y), which consults _coordinatedItems and falls
/// back to Model.SqFloorHeight - plain floor, so ZERO. RoomUser.Z, which is
/// what UserUpdate carries and what everything else renders against, comes
/// from SqAbsoluteHeight(x, y, GetAllRoomItemForSquare(x, y)) and is then
/// overridden by item.GetZ for seats and beds.
///
/// On a tile whose furniture is not in _coordinatedItems the first returns 0
/// while the second returns the real walk height. The client was told the edge
/// ran along Z = 0 while the avatar was standing at Z = 1.34, so the instant
/// V2 took ownership it wrote Z = 0 and the avatar dropped a tile and a third
/// in one frame - read as a pop or a speed-up, especially where two avatars
/// overlap and the drop is visible against a neighbour that has not moved.
///
/// This is the one function edge geometry asks. It is deliberately the same
/// query RoomUser.Z is derived from, because an edge that disagrees with the
/// avatar it describes is not a rounding problem - it is a different tile.
/// </summary>
public static class MovementHeights
{
    /// <summary>
    /// The authoritative walk height of a tile: the height an avatar standing
    /// on it actually occupies, furniture included.
    /// </summary>
    public static double Walk(Gamemap map, int x, int y)
    {
        if (map == null)
            return 0d;

        var items = map.GetAllRoomItemForSquare(x, y);

        // No furniture: the model's own floor height, which is the honest
        // answer and may legitimately be 0.
        if (items == null || items.Count == 0)
            return map.Model.SqFloorHeight[x, y];

        return map.SqAbsoluteHeight(x, y, items);
    }

    /// <summary>
    /// True when a carried height has drifted from the tile's authoritative
    /// walk height by enough to be seen. One hundredth of a tile is well below
    /// anything renderable and comfortably above floating-point noise.
    /// </summary>
    public static bool Disagrees(double carried, double authoritative) =>
        System.Math.Abs(carried - authoritative) > 0.01d;
}

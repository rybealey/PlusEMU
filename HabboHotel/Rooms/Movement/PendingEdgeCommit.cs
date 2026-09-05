using System.Drawing;

namespace Plus.HabboHotel.Rooms.Movement;

/// <summary>
/// pixelrp Movement V2: one edge's effect on a RoomUser, staged by the
/// scheduler and applied by the Q1 worker under RoomUserManager's _cycleLock.
///
/// This is the hand-off that lets V2 own route and timing while the EXISTING
/// UserUpdateComposer broadcast carries the result. For the first beta test
/// that means a stock client renders V2 movement natively - no packet 4110, no
/// client change and no patch-chain work required.
///
/// Immutable snapshot: once staged, the scheduler may move on without the
/// worker seeing a torn value.
/// </summary>
public readonly struct PendingEdgeCommit
{
    public readonly int VirtualId;

    /// <summary>Tile the avatar has now ARRIVED on (the previous edge's terminal).</summary>
    public readonly Point Tile;
    public readonly double TileZ;

    /// <summary>Tile the avatar is now walking TO, or Tile when the walk ended.</summary>
    public readonly Point EdgeTo;
    public readonly double EdgeToZ;

    public readonly byte Facing;

    /// <summary>False when the walk ended: the worker clears "mv" instead of setting it.</summary>
    public readonly bool IsMoving;

    public PendingEdgeCommit(
        int virtualId, Point tile, double tileZ, Point edgeTo, double edgeToZ, byte facing, bool isMoving)
    {
        VirtualId = virtualId;
        Tile = tile;
        TileZ = tileZ;
        EdgeTo = edgeTo;
        EdgeToZ = edgeToZ;
        Facing = facing;
        IsMoving = isMoving;
    }
}

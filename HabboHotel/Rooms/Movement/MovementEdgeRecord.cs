namespace Plus.HabboHotel.Rooms.Movement;

public readonly struct LookaheadTile
{
    public readonly int X;
    public readonly int Y;
    public readonly int Z100;

    public LookaheadTile(int x, int y, int z100)
    {
        X = x;
        Y = y;
        Z100 = z100;
    }
}

/// <summary>
/// pixelrp Movement V2: one edge, sealed by the scheduler and carrying
/// everything both the wire (packet 4110) and the server-truth commit need.
///
/// REPLACES PendingEdgeCommit, which was the V2 -> V1 bridge: it carried only
/// enough to mirror V1's RoomUser fields and then leaned on V1's UserUpdate
/// broadcast to render. That bridge is what made V1 and V2 coexist at runtime,
/// and coexistence caused both beta freezes - V1's tick could still reach a
/// walker and clear its "mv" out from under V2.
///
/// This is an immutable snapshot taken under the movement lock, so the outbound
/// worker can compose and send it without holding any lock and without reading
/// live MovementState that the scheduler may already have moved on from.
/// </summary>
public readonly struct MovementEdgeRecord
{
    public readonly int VirtualId;
    public readonly long WalkSessionId;
    public readonly int RouteRevision;
    public readonly int EdgeIndex;
    public readonly int Flags;
    public readonly int IntervalMs;

    /// <summary>Absolute movement-clock tick at which this edge starts.</summary>
    public readonly long CycleStartMs;

    public readonly int FromX;
    public readonly int FromY;
    public readonly int FromZ100;
    public readonly int ToX;
    public readonly int ToY;
    public readonly int ToZ100;

    public readonly int LookaheadCount;
    public readonly LookaheadTile[] Lookahead;

    /// <summary>Server-truth height of the arrival tile, for the commit.</summary>
    public readonly double ToZ;

    /// <summary>Facing for this edge, for the commit.</summary>
    public readonly byte Facing;

    public bool IsWalkEnd => (Flags & RpMovementV2Flags.WalkEnd) != 0;
    public bool IsDisplacement => (Flags & RpMovementV2Flags.Displacement) != 0;

    public MovementEdgeRecord(
        int virtualId, long walkSessionId, int routeRevision, int edgeIndex, int flags,
        int intervalMs, long cycleStartMs,
        int fromX, int fromY, int fromZ100,
        int toX, int toY, int toZ100, double toZ, byte facing,
        LookaheadTile[] lookahead, int lookaheadCount)
    {
        VirtualId = virtualId;
        WalkSessionId = walkSessionId;
        RouteRevision = routeRevision;
        EdgeIndex = edgeIndex;
        Flags = flags;
        IntervalMs = intervalMs;
        CycleStartMs = cycleStartMs;
        FromX = fromX;
        FromY = fromY;
        FromZ100 = fromZ100;
        ToX = toX;
        ToY = toY;
        ToZ100 = toZ100;
        ToZ = toZ;
        Facing = facing;
        Lookahead = lookahead;
        LookaheadCount = lookaheadCount;
    }

    public static int Z100(double z) => (int)Math.Round(z * 100);
}

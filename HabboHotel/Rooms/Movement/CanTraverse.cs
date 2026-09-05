using System.Drawing;
using Plus.HabboHotel.Items;

namespace Plus.HabboHotel.Rooms.Movement;

public enum TraverseResult : byte
{
    Blocked = 0,
    Allowed = 1,

    /// <summary>Door and last-step tiles: legal only as the final tile of a route.</summary>
    AllowedAsFinalOnly = 2
}

/// <summary>
/// Diagonal corner handling. LOCKED DEFAULT FOR ROLLOUT: <see cref="Off"/>.
///
/// V1 has NO corner check anywhere (verified: GameMap.IsValidStep / IsValidStep2
/// contain no orthogonal-neighbour test), so players can currently cut through a
/// fully sealed diagonal. Shipping Lenient in the same change as the new A*
/// would make two player-visible routing changes at once and neither would be
/// attributable. Off preserves today's routing; Lenient lands later, alone.
/// </summary>
public enum CornerPolicy : byte
{
    Off = 0,
    Lenient = 1, // at least one orthogonal side must be occupiable
    Strict = 2 // both orthogonal sides must be occupiable
}

/// <summary>Resolved ONCE per search/commit, never per tile.</summary>
public interface IGateAccess
{
    bool CanPass(int groupId);
}

public sealed class AllowAllGateAccess : IGateAccess
{
    public static readonly AllowAllGateAccess Instance = new();
    public bool CanPass(int groupId) => true;
}

public sealed class DenyAllGateAccess : IGateAccess
{
    public static readonly DenyAllGateAccess Instance = new();
    public bool CanPass(int groupId) => false;
}

/// <summary>
/// Everything the traversal rules need about the WALKER, resolved up front so
/// the predicate itself can stay pure and per-tile cheap.
/// </summary>
public readonly struct TraverseContext
{
    public readonly bool AllowOverride;
    public readonly bool IsRoller;
    public readonly bool IsMounted;
    public readonly bool DiagonalsAllowed;
    public readonly CornerPolicy CornerPolicy;
    public readonly IGateAccess Gates;

    public TraverseContext(
        bool allowOverride = false,
        bool isRoller = false,
        bool isMounted = false,
        bool diagonalsAllowed = true,
        CornerPolicy cornerPolicy = CornerPolicy.Off,
        IGateAccess? gates = null)
    {
        AllowOverride = allowOverride;
        IsRoller = isRoller;
        IsMounted = isMounted;
        DiagonalsAllowed = diagonalsAllowed;
        CornerPolicy = cornerPolicy;
        Gates = gates ?? AllowAllGateAccess.Instance;
    }
}

/// <summary>
/// pixelrp Movement V2 (A3): the ONE traversal predicate.
///
/// PURE. No mutation, no I/O, no logging, no packet sends, no item state changes.
/// This is the single most important property here: V1's commit-time validator
/// (Gamemap.IsValidStep2) cleared user.Path and set user.PathRecalcNeeded FROM
/// INSIDE A PREDICATE (GameMap.cs:797-828), which is why route state could
/// change as a side effect of merely asking whether a tile was walkable.
///
/// Search and commit MUST both go through <see cref="IsPassable"/> so they can
/// never disagree. V1 used two different functions with different rules, which
/// is the direct cause of its mid-walk `invalidStep` abort path.
///
/// ALLOCATION - honest note. The predicate itself allocates nothing, but the
/// height rule calls Gamemap.SqAbsoluteHeight, which internally allocates a
/// Point and a List (GameMap.cs:902-920). Callers that evaluate many tiles
/// (the A*) pass cached heights via the fromHeight/toHeight overload so that
/// cost is paid once per tile per search rather than per edge examined.
/// Making it truly allocation-free needs a maintained height map on Gamemap;
/// that is a follow-up, not a V2 blocker.
///
/// OCCUPANCY IS NEVER CONSULTED. Players and bots do not block any tile,
/// including as a route terminus. In V1 this was an accident of a dead feature
/// flag (RoomData.RoomBlockingEnabled is a hard-coded `get => true`, making
/// every occupancy branch unreachable); in V2 it is an explicit rule (I-10).
/// </summary>
public static class CanTraverse
{
    public const double MaxStepUp = 1.5;

    /// <summary>
    /// The SINGLE acceptance expression. Both the pathfinder and the commit
    /// path must use this - never re-derive it.
    /// </summary>
    public static bool IsPassable(TraverseResult result, bool isFinalStep) =>
        result == TraverseResult.Allowed ||
        (result == TraverseResult.AllowedAsFinalOnly && isFinalStep);

    /// <summary>
    /// Full evaluation, resolving heights from the map.
    /// </summary>
    public static TraverseResult Evaluate(
        Gamemap map, Point from, Point to, bool isFinalStep, in TraverseContext ctx)
    {
        if (map == null)
            return TraverseResult.Blocked;
        if (!InBounds(map, to.X, to.Y))
            return TraverseResult.Blocked;
        if (ctx.AllowOverride)
            return TraverseResult.Allowed;

        // Diagonal corner rule lives HERE and calls only EvaluateStep, never
        // itself - recursion is structurally impossible.
        var dx = to.X - from.X;
        var dy = to.Y - from.Y;
        if (dx != 0 && dy != 0)
        {
            if (!ctx.DiagonalsAllowed)
                return TraverseResult.Blocked;
            if (!DiagonalCornerOk(map, from, dx, dy, ctx))
                return TraverseResult.Blocked;
        }

        return EvaluateStep(map, from, to, isFinalStep, ctx,
            map.SqAbsoluteHeight(from.X, from.Y), map.SqAbsoluteHeight(to.X, to.Y));
    }

    /// <summary>
    /// Evaluation with caller-supplied heights, for callers (the A*) that
    /// already cache per-tile absolute heights for the duration of a search.
    /// </summary>
    public static TraverseResult Evaluate(
        Gamemap map, Point from, Point to, bool isFinalStep, in TraverseContext ctx,
        double fromHeight, double toHeight)
    {
        if (map == null)
            return TraverseResult.Blocked;
        if (!InBounds(map, to.X, to.Y))
            return TraverseResult.Blocked;
        if (ctx.AllowOverride)
            return TraverseResult.Allowed;

        var dx = to.X - from.X;
        var dy = to.Y - from.Y;
        if (dx != 0 && dy != 0)
        {
            if (!ctx.DiagonalsAllowed)
                return TraverseResult.Blocked;
            if (!DiagonalCornerOk(map, from, dx, dy, ctx))
                return TraverseResult.Blocked;
        }

        return EvaluateStep(map, from, to, isFinalStep, ctx, fromHeight, toHeight);
    }

    /// <summary>
    /// Orthogonal-only tile rules. Contains NO diagonal logic, which is what
    /// makes the corner test non-recursive.
    ///
    /// Rule order matters and mirrors V1: the guild gate is decided BEFORE the
    /// tile-state byte, because V1 returns true for a gate tile regardless of
    /// its state (GameMap.cs:864-869) and a member must not be blocked by the
    /// gate's own square state.
    /// </summary>
    private static TraverseResult EvaluateStep(
        Gamemap map, Point from, Point to, bool isFinalStep, in TraverseContext ctx,
        double fromHeight, double toHeight)
    {
        var items = map.GetAllRoomItemForSquare(to.X, to.Y);

        // 1. Guild gate - evaluated first, and PURELY. V1 additionally mutated
        //    the gate item (LegacyDataString/UpdateState/RequestUpdate) from
        //    inside the predicate; that belongs in the commit-time tile hook.
        var gate = FindGate(items);
        if (gate != null)
            return ctx.Gates.CanPass(gate.GroupId)
                ? TraverseResult.Allowed
                : TraverseResult.Blocked;

        // 2. Tile state byte. 0 = blocked, 1 = open, 2 = last step, 3 = door.
        var state = map.GameMap[to.X, to.Y];
        TraverseResult result;
        switch (state)
        {
            case 0:
                return TraverseResult.Blocked;
            case 2:
                result = TraverseResult.AllowedAsFinalOnly;
                break;
            case 3:
                // A seat on a door tile is walkable mid-route (V1 chair rule).
                result = HighestIsSeat(items)
                    ? TraverseResult.Allowed
                    : TraverseResult.AllowedAsFinalOnly;
                break;
            default:
                result = TraverseResult.Allowed;
                break;
        }

        // 3. Height. Drops are unrestricted, matching V1.
        if (!ctx.IsRoller && !ctx.IsMounted && (toHeight - fromHeight) > MaxStepUp)
            return TraverseResult.Blocked;

        // 4. Occupancy is NEVER consulted (I-10). Stacking stays legal.
        return result;
    }

    /// <summary>
    /// Is an orthogonal side tile solid enough to permit cutting the corner?
    ///
    /// isFinalStep is deliberately FALSE: a corner is passed, never landed on,
    /// so a door / last-step tile does not qualify as ground for a diagonal.
    /// </summary>
    private static bool CanOccupyOrthogonalSide(
        Gamemap map, Point from, Point side, in TraverseContext ctx)
    {
        if (!InBounds(map, side.X, side.Y))
            return false;
        return EvaluateStep(map, from, side, false, ctx,
            map.SqAbsoluteHeight(from.X, from.Y),
            map.SqAbsoluteHeight(side.X, side.Y)) == TraverseResult.Allowed;
    }

    private static bool DiagonalCornerOk(
        Gamemap map, Point from, int dx, int dy, in TraverseContext ctx)
    {
        if (ctx.CornerPolicy == CornerPolicy.Off)
            return true;

        var sideA = new Point(from.X + dx, from.Y);
        var sideB = new Point(from.X, from.Y + dy);
        var aOk = CanOccupyOrthogonalSide(map, from, sideA, ctx);
        var bOk = CanOccupyOrthogonalSide(map, from, sideB, ctx);

        return ctx.CornerPolicy == CornerPolicy.Strict ? aOk && bOk : aOk || bOk;
    }

    public static bool InBounds(Gamemap map, int x, int y)
    {
        if (x < 0 || y < 0)
            return false;
        var model = map.Model;
        if (model == null)
            return false;
        return x < model.MapSizeX && y < model.MapSizeY;
    }

    private static Item? FindGate(List<Item> items)
    {
        if (items == null || items.Count == 0)
            return null;
        for (var i = 0; i < items.Count; i++)
        {
            var item = items[i];
            if (item?.Definition != null && item.Definition.InteractionType == InteractionType.GuildGate)
                return item;
        }
        return null;
    }

    private static bool HighestIsSeat(List<Item> items)
    {
        if (items == null || items.Count == 0)
            return false;
        var chair = false;
        double highestZ = -1;
        for (var i = 0; i < items.Count; i++)
        {
            var item = items[i];
            if (item?.Definition == null)
                continue;
            if (item.GetZ < highestZ)
                continue;
            highestZ = item.GetZ;
            chair = item.Definition.IsSeat;
        }
        return chair;
    }
}

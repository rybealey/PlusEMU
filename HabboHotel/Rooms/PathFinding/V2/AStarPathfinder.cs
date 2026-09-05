using System.Drawing;
using Plus.HabboHotel.Rooms.Movement;

namespace Plus.HabboHotel.Rooms.PathFinding.V2;

public enum PathResult : byte
{
    /// <summary>A complete route to the requested goal.</summary>
    Complete = 0,

    /// <summary>Goal unreachable / budget exhausted; route ends at the closest reachable tile.</summary>
    Partial = 1,

    /// <summary>No route at all (start == goal, out of bounds, or fully boxed in).</summary>
    None = 2
}

/// <summary>
/// pixelrp Movement V2 (A3): a CORRECT A*.
///
/// Every one of V1's four search defects is fixed here, deliberately:
///
///   V1 PathFinder.cs:79   cost = current.Cost + diff + GetDistanceSquared(end)
///                         -> the heuristic was ADDED INTO the stored cost and
///                            therefore ACCUMULATED along the path. That is not
///                            A*; g and h are separated here.
///   V1 PathFinder.cs:87   returned the goal the moment it was GENERATED.
///                         -> here the goal is accepted only when EXTRACTED,
///                            which is what makes the result optimal.
///   V1 (MinHeap)          no decrease-key, so improved costs never re-sorted.
///                         -> PathScratch.DecreaseKey maintains the heap.
///   V1 PathFinder.cs:45   full node array allocated per search.
///                         -> generation-stamped preallocated scratch.
///
/// Costs are integers: 10 orthogonal, 14 diagonal (~10*sqrt2). The octile
/// heuristic is admissible and consistent for this cost pair, so A* is optimal.
/// </summary>
public static class AStarPathfinder
{
    public const int OrthoCost = 10;
    public const int DiagCost = 14;

    /// <summary>
    /// FROZEN neighbour order: N, NE, E, SE, S, SW, W, NW.
    ///
    /// This array decides which of several equal-cost routes wins, so it is part
    /// of observable behaviour: reordering it changes how every avatar in the
    /// hotel walks. Do not touch it casually.
    /// </summary>
    private static readonly (int dx, int dy)[] Neighbours8 =
    {
        (0, -1), (1, -1), (1, 0), (1, 1), (0, 1), (-1, 1), (-1, 0), (-1, -1)
    };

    private static readonly (int dx, int dy)[] Neighbours4 =
    {
        (0, -1), (1, 0), (0, 1), (-1, 0)
    };

    /// <summary>Octile distance. Admissible and consistent for 10/14 costs.</summary>
    public static int Heuristic(int ax, int ay, int bx, int by, bool diagonals)
    {
        var dx = Math.Abs(ax - bx);
        var dy = Math.Abs(ay - by);
        if (!diagonals)
            return OrthoCost * (dx + dy);
        return OrthoCost * (dx + dy) + (DiagCost - 2 * OrthoCost) * Math.Min(dx, dy);
    }

    /// <summary>
    /// Search from <paramref name="start"/> to <paramref name="goal"/>.
    ///
    /// <paramref name="allowPartial"/> is the PRODUCT DECISION seam: when true an
    /// unreachable goal yields the closest reachable tile instead of nothing.
    /// It is isolated here precisely so that decision cannot block this work.
    /// </summary>
    public static PathResult FindRoute(
        Gamemap map,
        PathScratch scratch,
        RouteBuffer route,
        Point start,
        Point goal,
        in TraverseContext ctx,
        int baseIndex,
        bool allowPartial,
        int maxExpansions = 0)
    {
        route.Clear();
        MovementCounters.PathfindCall();

        if (map == null || map.Model == null)
        {
            MovementCounters.PathfindFailed();
            return PathResult.None;
        }
        if (!CanTraverse.InBounds(map, start.X, start.Y) || !CanTraverse.InBounds(map, goal.X, goal.Y))
        {
            MovementCounters.PathfindFailed();
            return PathResult.None;
        }
        if (start.X == goal.X && start.Y == goal.Y)
            return PathResult.None;

        var diagonals = ctx.DiagonalsAllowed && map.DiagonalEnabled;
        var neighbours = diagonals ? Neighbours8 : Neighbours4;
        if (maxExpansions <= 0)
            maxExpansions = Math.Max(2000, 4 * scratch.CellCount);

        scratch.NewGeneration();

        var startCell = scratch.Index(start.X, start.Y);
        var goalCell = scratch.Index(goal.X, goal.Y);

        scratch.SetNode(startCell, 0, Heuristic(start.X, start.Y, goal.X, goal.Y, diagonals), -1);
        scratch.Push(startCell);

        var bestCell = startCell;
        var bestH = scratch.H(startCell);
        var expansions = 0;
        var found = false;

        while (scratch.HeapCount > 0)
        {
            var current = scratch.Pop();

            // ACCEPTANCE ON EXTRACTION - not on generation. This is what makes
            // the returned route optimal rather than merely first-found.
            if (current == goalCell)
            {
                found = true;
                break;
            }

            scratch.Close(current);
            if (++expansions > maxExpansions)
                break;

            var cx = scratch.XOf(current);
            var cy = scratch.YOf(current);
            var from = new Point(cx, cy);
            var fromHeight = HeightOf(map, scratch, current, cx, cy);
            var currentG = scratch.G(current);

            for (var i = 0; i < neighbours.Length; i++)
            {
                var nx = cx + neighbours[i].dx;
                var ny = cy + neighbours[i].dy;
                if (!CanTraverse.InBounds(map, nx, ny))
                    continue;

                var neighbourCell = scratch.Index(nx, ny);
                if (scratch.Closed(neighbourCell))
                    continue;

                var isFinal = neighbourCell == goalCell;
                var to = new Point(nx, ny);
                var toHeight = HeightOf(map, scratch, neighbourCell, nx, ny);

                var verdict = CanTraverse.Evaluate(map, from, to, isFinal, ctx, fromHeight, toHeight);
                if (!CanTraverse.IsPassable(verdict, isFinal))
                    continue;

                var isDiagonal = neighbours[i].dx != 0 && neighbours[i].dy != 0;
                var tentativeG = currentG + (isDiagonal ? DiagCost : OrthoCost);

                var alreadySeen = scratch.Seen(neighbourCell);
                if (alreadySeen && tentativeG >= scratch.G(neighbourCell))
                    continue;

                var h = Heuristic(nx, ny, goal.X, goal.Y, diagonals);
                var wasInHeap = alreadySeen && scratch.InHeap(neighbourCell);
                scratch.SetNode(neighbourCell, tentativeG, h, current);

                if (wasInHeap)
                    scratch.DecreaseKey(neighbourCell);
                else
                    scratch.Push(neighbourCell);

                if (h < bestH)
                {
                    bestH = h;
                    bestCell = neighbourCell;
                }
            }
        }

        if (!found)
        {
            if (!allowPartial || bestCell == startCell)
            {
                MovementCounters.PathfindFailed();
                return PathResult.None;
            }
            MovementCounters.PathfindPartial();
            return Reconstruct(scratch, route, startCell, bestCell, baseIndex, true)
                ? PathResult.Partial
                : PathResult.None;
        }

        return Reconstruct(scratch, route, startCell, goalCell, baseIndex, false)
            ? PathResult.Complete
            : PathResult.None;
    }

    private static double HeightOf(Gamemap map, PathScratch scratch, int cell, int x, int y)
    {
        if (scratch.TryGetHeight(cell, out var cached))
            return cached;
        var height = map.SqAbsoluteHeight(x, y);
        scratch.SetHeight(cell, height);
        return height;
    }

    /// <summary>
    /// Walk parent links back from the end cell, then flip to START-FIRST.
    /// The start tile is excluded: route[0] is the first tile stepped ONTO.
    /// </summary>
    private static bool Reconstruct(
        PathScratch scratch, RouteBuffer route, int startCell, int endCell, int baseIndex, bool partial)
    {
        var count = 0;
        for (var cell = endCell; cell != startCell && cell >= 0; cell = scratch.Parent(cell))
        {
            count++;
            if (count > scratch.CellCount)
                return false; // parent-link cycle: refuse rather than hang
        }
        if (count == 0)
            return false;

        // Explicitly typed: a conditional with a stackalloc branch only converts
        // when the target type is Span<T>, and `var` would not infer it.
        Span<Point> reversed = count <= 256 ? stackalloc Point[count] : new Point[count];
        var index = 0;
        for (var cell = endCell; cell != startCell && cell >= 0; cell = scratch.Parent(cell))
            reversed[index++] = new Point(scratch.XOf(cell), scratch.YOf(cell));

        route.SetFromReversed(reversed, count, baseIndex, partial);
        return true;
    }
}

using System.Drawing;

namespace Plus.HabboHotel.Rooms.PathFinding.V2;

/// <summary>
/// pixelrp Movement V2 (A3/A5): a route, START-FIRST.
///
///   Tiles[0]            = the FIRST tile to step onto (never the start tile)
///   Tiles[Length - 1]   = the goal
///
/// V1 returned a GOAL-FIRST list, which is why every consumer indexed it as
/// <c>Path[Path.Count - PathStep - 1]</c> - an expression that appeared in
/// V1, whose Path/PathStep fields on RoomUser were deleted on 2026-09-21.
/// Quoted because the expression is the point, not because it still exists:
/// it appeared in
/// ProcessUserMovement, both formation functions and the lookahead peek, i.e.
/// four independent chances to get an off-by-one wrong. Start-first removes the
/// whole class.
///
/// BaseIndex is the walk's EdgeIndex that Tiles[0] was PLANNED for.
///
/// IT IS A DIAGNOSTIC LABEL AND NOTHING MAY STAGE FROM IT. The only source for
/// a staged record's index is MovementState.EdgeIndex, which StageEdge reads
/// directly; BaseIndex has exactly one reader in the codebase,
/// MovementReplanTrace.ReadEdgeGeometry, which needs it to say which index a
/// route tile belonged to BEFORE the pathfinder overwrote the buffer.
///
/// It cannot be derived from EdgeIndex and Cursor, which is why it still
/// exists: those advance at different moments - EdgeIndex in
/// CommitEdgeSilently, Cursor in PlanNextEdge - and SyncCommitsTo advances
/// EdgeIndex alone, so their offset is not fixed.
///
/// It CAN disagree with EdgeIndex, and has: a redirect labels the route e + 1
/// while planning it from the terminal of edge EdgeIndex, and those are the
/// same edge only when EdgeIndex == e. Code that published under this label
/// instead of under EdgeIndex put one pair of tiles on the wire as two
/// indexes and tore a one-tile hole in the chain. Read it to describe the
/// past; never to address the future.
/// </summary>
public sealed class RouteBuffer
{
    private Point[] _tiles;

    public int Length { get; private set; }

    /// <summary>Cursor into <see cref="Tiles"/>: the next tile to be emitted.</summary>
    public int Cursor { get; private set; }

    /// <summary>The EdgeIndex that Tiles[0] represents.</summary>
    public int BaseIndex { get; private set; }

    /// <summary>True when the search stopped short of the requested target.</summary>
    public bool IsPartial { get; private set; }

    public RouteBuffer(int capacity = 128) => _tiles = new Point[Math.Max(8, capacity)];

    public Point this[int index] => _tiles[index];
    public bool HasNext => Cursor < Length;
    public Point PeekNext() => _tiles[Cursor];
    public bool IsLast => Cursor == Length - 1;
    public void Advance() => Cursor++;

    public void Clear()
    {
        Length = 0;
        Cursor = 0;
        BaseIndex = 0;
        IsPartial = false;
    }

    public void EnsureCapacity(int required)
    {
        if (required <= _tiles.Length)
            return;
        var size = _tiles.Length;
        while (size < required)
            size <<= 1;
        Array.Resize(ref _tiles, size);
    }

    /// <summary>
    /// Fill from a reversed (goal-first) walk of parent links, flipping it to
    /// start-first. <paramref name="count"/> excludes the start tile.
    /// </summary>
    public void SetFromReversed(Span<Point> reversedExcludingStart, int count, int baseIndex, bool partial)
    {
        EnsureCapacity(count);
        for (var i = 0; i < count; i++)
            _tiles[i] = reversedExcludingStart[count - 1 - i];
        Length = count;
        Cursor = 0;
        BaseIndex = baseIndex;
        IsPartial = partial;
    }
}

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
/// THERE IS NO ROUTE-SIDE EDGE LABEL, and deliberately so. A BaseIndex field
/// lived here until 2026-09-22, carrying the EdgeIndex that Tiles[0] was
/// planned for. It was removed with its last reader
/// (MovementReplanTrace.ReadEdgeGeometry) rather than left dead.
///
/// IF IT IS EVER WANTED BACK, the reason it was dangerous is worth keeping:
/// it could disagree with MovementState.EdgeIndex, and did. A redirect labels
/// the route e + 1 while planning it from the terminal of edge EdgeIndex, and
/// those are the same edge only when EdgeIndex == e. Code that published under
/// that label instead of under EdgeIndex put one pair of tiles on the wire as
/// two indexes and tore a one-tile hole in the chain. The only source for a
/// staged record's index is MovementState.EdgeIndex, which StageEdge reads
/// directly.
/// </summary>
public sealed class RouteBuffer
{
    private Point[] _tiles;

    public int Length { get; private set; }

    /// <summary>Cursor into <see cref="Tiles"/>: the next tile to be emitted.</summary>
    public int Cursor { get; private set; }

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
    /// Put one ALREADY-PROMISED tile back at the front of the route.
    ///
    /// FOR TWO CALLERS, and the same reason in both. MovementController.
    /// StopAfterCurrentStep uses it on an emptied route to keep ONLY the step
    /// the client is about to begin. The first: a redirect that must not
    /// rewrite the edge the client is about to start drawing. That redirect plans from the promised
    /// edge's DESTINATION rather than from where the walker is, so the route it
    /// gets back begins one tile too far along. This puts the promised tile
    /// back on the front, and the route then reads exactly as it would have if
    /// only the tiles after it had changed.
    ///
    /// That is the whole point: everything downstream - PlanNextEdge, StageEdge,
    /// the lookahead and the early correction - keeps working unchanged,
    /// because the shape it sees is the shape it has always seen.
    ///
    /// NOT a general insert. It restores a tile the walker was already
    /// committed to, which is why it cannot make the route illegal: that step
    /// was validated when it was first planned and nothing has moved since.
    /// </summary>
    public void PrependPromised(Point tile)
    {
        EnsureCapacity(Length + 1);
        for (var i = Length; i > Cursor; i--)
            _tiles[i] = _tiles[i - 1];
        _tiles[Cursor] = tile;
        Length++;
    }

    /// <summary>
    /// Fill from a reversed (goal-first) walk of parent links, flipping it to
    /// start-first. <paramref name="count"/> excludes the start tile.
    /// </summary>
    public void SetFromReversed(Span<Point> reversedExcludingStart, int count, bool partial)
    {
        EnsureCapacity(count);
        for (var i = 0; i < count; i++)
            _tiles[i] = reversedExcludingStart[count - 1 - i];
        Length = count;
        Cursor = 0;
        IsPartial = partial;
    }
}

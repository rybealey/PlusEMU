using System.Drawing;

namespace Plus.HabboHotel.Rooms.PathFinding.V2;

/// <summary>
/// pixelrp Movement V2 (A3/A5): a route, START-FIRST.
///
///   Tiles[0]            = the FIRST tile to step onto (never the start tile)
///   Tiles[Length - 1]   = the goal
///
/// V1 returned a GOAL-FIRST list, which is why every consumer indexed it as
/// <c>Path[Path.Count - PathStep - 1]</c> - an expression that appears in
/// ProcessUserMovement, both formation functions and the lookahead peek, i.e.
/// four independent chances to get an off-by-one wrong. Start-first removes the
/// whole class.
///
/// BaseIndex is the walk's EdgeIndex that Tiles[0] corresponds to. On a
/// redirect the new route describes edges from e+1 onward, so BaseIndex = e + 1
/// (LOCK NOTE 2.2). It is what lets promised future indexes be matched against
/// the route without a separate promise ring.
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

    /// <summary>The edge index that <see cref="PeekNext"/> would occupy.</summary>
    public int NextEdgeIndex => BaseIndex + Cursor;

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

    public Point Goal => Length > 0 ? _tiles[Length - 1] : Point.Empty;
}

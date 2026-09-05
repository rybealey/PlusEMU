namespace Plus.HabboHotel.Rooms.PathFinding.V2;

/// <summary>
/// pixelrp Movement V2 (A3): per-room preallocated A* working set, including an
/// INDEXED binary min-heap with a real decrease-key.
///
/// Two V1 defects are fixed structurally here:
///
///  1. ALLOCATION. V1's FindPathReversed allocated a fresh
///     PathFinderNode[MapSizeX, MapSizeY] on EVERY search (PathFinder.cs:45) -
///     2500 references per call on a 50x50 model, garbage every click. Here the
///     arrays are allocated once per room and invalidated with a generation
///     stamp, so a search costs no allocation and no clearing pass.
///
///  2. STALE HEAP PRIORITY. V1 lowered a node's Cost after it was already in
///     the heap but had no way to re-sort it (MinHeap has no decrease-key), so
///     the open list silently went out of order. Here Push/DecreaseKey maintain
///     the invariant, and HeapPos tracks each cell's slot.
///
/// Cells are addressed as a flat index y * Width + x.
///
/// THREADING: owned by the room and only ever touched under the room's movement
/// lock. It is deliberately NOT thread-safe.
/// </summary>
public sealed class PathScratch
{
    public readonly int Width;
    public readonly int Height;
    private readonly int _cells;

    // Per-cell search state, valid only where Stamp[cell] == _generation.
    private readonly int[] _g;
    private readonly int[] _h;
    private readonly int[] _f;
    private readonly int[] _order;
    private readonly int[] _parent;
    private readonly ushort[] _seenStamp;
    private readonly ushort[] _closedStamp;

    // Cached absolute tile heights for the duration of one search.
    private readonly double[] _heightCache;
    private readonly ushort[] _heightStamp;

    // Indexed binary min-heap over cell indices.
    private readonly int[] _heap;
    private readonly int[] _heapPos;
    private int _heapCount;

    private ushort _generation;
    private int _orderCounter;

    public PathScratch(int width, int height)
    {
        Width = width;
        Height = height;
        _cells = width * height;

        _g = new int[_cells];
        _h = new int[_cells];
        _f = new int[_cells];
        _order = new int[_cells];
        _parent = new int[_cells];
        _seenStamp = new ushort[_cells];
        _closedStamp = new ushort[_cells];
        _heightCache = new double[_cells];
        _heightStamp = new ushort[_cells];
        _heap = new int[_cells];
        _heapPos = new int[_cells];
        _generation = 0;
    }

    public int CellCount => _cells;
    public int HeapCount => _heapCount;
    public int Index(int x, int y) => y * Width + x;
    public int XOf(int cell) => cell % Width;
    public int YOf(int cell) => cell / Width;

    /// <summary>
    /// O(1) invalidation of the whole working set. On stamp wraparound the
    /// arrays are cleared once so a stale stamp can never read as current.
    /// </summary>
    public void NewGeneration()
    {
        _heapCount = 0;
        _orderCounter = 0;

        if (_generation == ushort.MaxValue)
        {
            Array.Clear(_seenStamp, 0, _cells);
            Array.Clear(_closedStamp, 0, _cells);
            Array.Clear(_heightStamp, 0, _cells);
            _generation = 0;
        }
        _generation++;
    }

    public bool Seen(int cell) => _seenStamp[cell] == _generation;
    public bool Closed(int cell) => _closedStamp[cell] == _generation;
    public void Close(int cell) => _closedStamp[cell] = _generation;
    public int G(int cell) => _g[cell];
    public int H(int cell) => _h[cell];
    public int Parent(int cell) => _parent[cell];

    public void SetNode(int cell, int g, int h, int parent)
    {
        _g[cell] = g;
        _h[cell] = h;
        _f[cell] = g + h;
        _parent[cell] = parent;
        if (_seenStamp[cell] != _generation)
        {
            _seenStamp[cell] = _generation;
            _order[cell] = ++_orderCounter;
            _heapPos[cell] = -1;
        }
    }

    /// <summary>Per-search absolute-height cache; see the note in CanTraverse.</summary>
    public bool TryGetHeight(int cell, out double height)
    {
        if (_heightStamp[cell] == _generation)
        {
            height = _heightCache[cell];
            return true;
        }
        height = 0;
        return false;
    }

    public void SetHeight(int cell, double height)
    {
        _heightCache[cell] = height;
        _heightStamp[cell] = _generation;
    }

    // ---- indexed min-heap -------------------------------------------------

    public bool InHeap(int cell) => _seenStamp[cell] == _generation && _heapPos[cell] >= 0;

    /// <summary>
    /// Total order: f ascending, then h ascending (prefer nodes nearer the goal
    /// among equal f - fewer expansions and straighter-looking routes), then
    /// insertion order. The final term is what makes two identical searches
    /// return the IDENTICAL route; V1's plain binary heap was unstable for ties,
    /// which is why two avatars on one corridor could pick different equal-cost
    /// paths.
    /// </summary>
    private bool Less(int a, int b)
    {
        if (_f[a] != _f[b]) return _f[a] < _f[b];
        if (_h[a] != _h[b]) return _h[a] < _h[b];
        return _order[a] < _order[b];
    }

    public void Push(int cell)
    {
        var pos = _heapCount++;
        _heap[pos] = cell;
        _heapPos[cell] = pos;
        SiftUp(pos);
    }

    public int Pop()
    {
        var top = _heap[0];
        _heapPos[top] = -1;
        _heapCount--;
        if (_heapCount > 0)
        {
            var moved = _heap[_heapCount];
            _heap[0] = moved;
            _heapPos[moved] = 0;
            SiftDown(0);
        }
        return top;
    }

    /// <summary>Re-sort a cell whose f just decreased. The V1 gap.</summary>
    public void DecreaseKey(int cell)
    {
        var pos = _heapPos[cell];
        if (pos >= 0)
            SiftUp(pos);
    }

    private void SiftUp(int pos)
    {
        var cell = _heap[pos];
        while (pos > 0)
        {
            var parentPos = (pos - 1) >> 1;
            var parentCell = _heap[parentPos];
            if (!Less(cell, parentCell))
                break;
            _heap[pos] = parentCell;
            _heapPos[parentCell] = pos;
            pos = parentPos;
        }
        _heap[pos] = cell;
        _heapPos[cell] = pos;
    }

    private void SiftDown(int pos)
    {
        var cell = _heap[pos];
        while (true)
        {
            var left = (pos << 1) + 1;
            if (left >= _heapCount)
                break;
            var right = left + 1;
            var best = left;
            if (right < _heapCount && Less(_heap[right], _heap[left]))
                best = right;
            var bestCell = _heap[best];
            if (!Less(bestCell, cell))
                break;
            _heap[pos] = bestCell;
            _heapPos[bestCell] = pos;
            pos = best;
        }
        _heap[pos] = cell;
        _heapPos[cell] = pos;
    }
}

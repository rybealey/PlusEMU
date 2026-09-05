namespace Plus.HabboHotel.Rooms.Movement;

/// <summary>
/// A node that can live in exactly one <see cref="IndexedDueHeap{T}"/>.
/// HeapIndex is owned by the heap; -1 means "not present".
/// </summary>
public interface IDueHeapNode
{
    int HeapIndex { get; set; }
    long DueTick { get; set; }
}

/// <summary>
/// pixelrp Movement V2 (A6): a min-heap keyed by due time in which an element
/// can appear AT MOST ONCE.
///
/// This is the structure that enforces two locked invariants:
///   "exactly ONE heap entry per active room"
///   "exactly ONE scheduler entry per moving walker"    (I-1)
///
/// The architecture's own pseudo-code leaked duplicates because it used a bare
/// Push on every pass AND processed signalled rooms without popping their
/// existing entry. There is deliberately NO Push method here - the only ways in
/// are InsertOrUpdate and Remove, so a duplicate cannot be expressed.
///
/// Ties break on insertion sequence, so drain order is deterministic and
/// replayable in tests.
///
/// NOT thread-safe: the room heap is owned by the scheduler thread, and each
/// walker heap is owned by its room's movement lock.
/// </summary>
public sealed class IndexedDueHeap<T> where T : class, IDueHeapNode
{
    private T?[] _items;
    private long[] _sequence;
    private long _sequenceCounter;

    public int Count { get; private set; }

    public IndexedDueHeap(int capacity = 32)
    {
        capacity = Math.Max(4, capacity);
        _items = new T?[capacity];
        _sequence = new long[capacity];
    }

    public bool Contains(T item) => item.HeapIndex >= 0 && item.HeapIndex < Count && ReferenceEquals(_items[item.HeapIndex], item);

    public long PeekDue => Count > 0 ? _items[0]!.DueTick : long.MaxValue;

    public T? Peek() => Count > 0 ? _items[0] : null;

    /// <summary>
    /// Insert the item, or move it if it is already present. The ONLY way in.
    /// </summary>
    public void InsertOrUpdate(T item, long dueTick)
    {
        if (Contains(item))
        {
            var oldDue = item.DueTick;
            item.DueTick = dueTick;
            var pos = item.HeapIndex;
            if (dueTick < oldDue)
                SiftUp(pos);
            else if (dueTick > oldDue)
                SiftDown(pos);
            return;
        }

        item.DueTick = dueTick;
        if (Count == _items.Length)
            Grow();

        var index = Count++;
        _items[index] = item;
        _sequence[index] = ++_sequenceCounter;
        item.HeapIndex = index;
        SiftUp(index);
    }

    public void Remove(T item)
    {
        if (!Contains(item))
        {
            item.HeapIndex = -1;
            return;
        }

        var pos = item.HeapIndex;
        item.HeapIndex = -1;
        Count--;

        if (pos == Count)
        {
            _items[Count] = null;
            return;
        }

        var moved = _items[Count]!;
        var movedSeq = _sequence[Count];
        _items[Count] = null;
        _items[pos] = moved;
        _sequence[pos] = movedSeq;
        moved.HeapIndex = pos;

        SiftDown(pos);
        SiftUp(moved.HeapIndex);
    }

    public T? Pop()
    {
        if (Count == 0)
            return null;
        var top = _items[0]!;
        Remove(top);
        return top;
    }

    public void Clear()
    {
        for (var i = 0; i < Count; i++)
        {
            if (_items[i] != null)
                _items[i]!.HeapIndex = -1;
            _items[i] = null;
        }
        Count = 0;
    }

    /// <summary>Snapshot for the watchdog / tests. Allocates; not for the hot path.</summary>
    public List<T> ToList()
    {
        var list = new List<T>(Count);
        for (var i = 0; i < Count; i++)
        {
            if (_items[i] != null)
                list.Add(_items[i]!);
        }
        return list;
    }

    private bool Less(int a, int b)
    {
        var da = _items[a]!.DueTick;
        var db = _items[b]!.DueTick;
        if (da != db) return da < db;
        return _sequence[a] < _sequence[b];
    }

    private void Grow()
    {
        var size = _items.Length << 1;
        Array.Resize(ref _items, size);
        Array.Resize(ref _sequence, size);
    }

    private void SiftUp(int pos)
    {
        while (pos > 0)
        {
            var parent = (pos - 1) >> 1;
            if (!Less(pos, parent))
                break;
            Swap(pos, parent);
            pos = parent;
        }
    }

    private void SiftDown(int pos)
    {
        while (true)
        {
            var left = (pos << 1) + 1;
            if (left >= Count)
                break;
            var right = left + 1;
            var best = (right < Count && Less(right, left)) ? right : left;
            if (!Less(best, pos))
                break;
            Swap(pos, best);
            pos = best;
        }
    }

    private void Swap(int a, int b)
    {
        (_items[a], _items[b]) = (_items[b], _items[a]);
        (_sequence[a], _sequence[b]) = (_sequence[b], _sequence[a]);
        _items[a]!.HeapIndex = a;
        _items[b]!.HeapIndex = b;
    }
}

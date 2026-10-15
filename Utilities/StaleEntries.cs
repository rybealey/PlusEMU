using System.Collections.Concurrent;

namespace Plus.Utilities;

/// <summary>
/// pixelrp: keeps a "when did this player last do X" map from growing for
/// as long as the server is up.
///
/// Cooldowns and rate limits live in dictionaries keyed by player that are
/// static, or held by a command object that lasts the whole uptime, and
/// nothing ever took an entry back out - so every player who had ever used
/// the command stayed in the map until the next restart. Clearing an entry
/// at logout would be the wrong fix: a relog would then skip the cooldown.
/// So an entry goes only once it can no longer matter (its cooldown has run
/// out, its jam has ended), and a map is only walked once it is big enough
/// to be worth it - the shape NotificationUtility.Debounce already uses.
/// </summary>
public static class StaleEntries
{
    /// <summary>How many entries a map may hold before it is swept.</summary>
    public const int SweepAbove = 256;

    /// <summary>
    /// Once <paramref name="map"/> holds more than <see cref="SweepAbove"/>
    /// entries, remove every one <paramref name="isStale"/> says no longer
    /// matters. An entry renewed while the sweep runs is kept: it is removed
    /// only if it still holds the value that was judged stale.
    /// </summary>
    public static void Sweep<TKey, TValue>(ConcurrentDictionary<TKey, TValue> map, Func<TKey, TValue, bool> isStale)
        where TKey : notnull
    {
        if (map.Count <= SweepAbove)
            return;
        foreach (var (key, value) in map)
        {
            if (isStale(key, value))
                map.TryRemove(new KeyValuePair<TKey, TValue>(key, value));
        }
    }
}

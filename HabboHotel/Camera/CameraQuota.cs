using System.Collections.Concurrent;

namespace Plus.HabboHotel.Camera;

/// <summary>
/// pixelrp: a ceiling on how fast one player can have the server write photo
/// files. Each save/edit writes image bytes straight from the client to disk,
/// and nothing capped the rate, so a loop could fill the disk. Generous for
/// real editing (a save a second is fine); only a flood is turned away. Per
/// session and in memory - a relog resets it, which is acceptable for this.
/// </summary>
public static class CameraQuota
{
    private const int WindowMs = 60_000;
    private const int MaxPerWindow = 20;
    private const int MinGapMs = 1_000;

    private static readonly ConcurrentDictionary<int, (long WindowStart, int Count, long Last)> Recent = new();

    /// <summary>True if this player may save a photo now; records the save if so.</summary>
    public static bool TryTake(int userId)
    {
        var now = Environment.TickCount64;
        var ok = true;
        Recent.AddOrUpdate(userId,
            _ => (now, 1, now),
            (_, prev) =>
            {
                if (now - prev.Last < MinGapMs)
                {
                    ok = false;
                    return prev;
                }
                if (now - prev.WindowStart >= WindowMs)
                    return (now, 1, now);
                if (prev.Count >= MaxPerWindow)
                {
                    ok = false;
                    return prev;
                }
                return (prev.WindowStart, prev.Count + 1, now);
            });
        return ok;
    }
}

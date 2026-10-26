using System.Collections.Concurrent;

namespace Plus.Utilities;

/// <summary>
/// pixelrp: a per-player command cooldown that is fair to a slow connection -
/// the :hit, :slap, :stun, :push and :spush timers.
///
/// A player who uses a command again the moment its cooldown is up sends it
/// over a connection whose delay wobbles. When the last use arrived a little
/// late and this one a little early, the server saw the gap come up short -
/// 2.8 seconds of :hit's three - and refused it, which on a slow connection
/// happened fight after fight. Up to <see cref="JitterAllowance"/> early is
/// let through, and the next cooldown then runs from when this one was due,
/// not from when the command arrived, so over a whole fight nobody uses a
/// command any faster than its cooldown allows.
///
/// Commands are DI singletons, so one of these held by a command is shared
/// hotel-wide; concurrent because rooms tick on their own threads.
/// </summary>
public sealed class PlayerCooldown
{
    /// <summary>How early a use may arrive and still go ahead.</summary>
    public static readonly TimeSpan JitterAllowance = TimeSpan.FromMilliseconds(250);

    /// <summary>When each player's cooldown runs out.</summary>
    private readonly ConcurrentDictionary<int, DateTime> _readyAt = new();

    public PlayerCooldown(int seconds) => Seconds = seconds;

    public int Seconds { get; }

    /// <summary>
    /// True while <paramref name="playerId"/> must still wait, with the whole
    /// seconds left - rounded up, so it never reads 0 while the gate is shut.
    /// </summary>
    public bool IsWaiting(int playerId, out int secondsLeft)
    {
        var now = DateTime.UtcNow;
        if (_readyAt.TryGetValue(playerId, out var readyAt) && now < readyAt - JitterAllowance)
        {
            // never more than the cooldown itself: a use let in early books
            // the next one from when it was due, a moment past a full wait
            secondsLeft = Math.Min(Seconds, (int)Math.Ceiling((readyAt - now).TotalSeconds));
            return true;
        }
        secondsLeft = 0;
        return false;
    }

    /// <summary>
    /// Starts the cooldown for a use that went ahead - from when the last one
    /// ran out, if this use was let in early.
    /// </summary>
    public void Start(int playerId)
    {
        var now = DateTime.UtcNow;
        var from = _readyAt.TryGetValue(playerId, out var readyAt) && readyAt > now ? readyAt : now;
        _readyAt[playerId] = from.AddSeconds(Seconds);
        // and nobody whose cooldown has run out stays in the map (StaleEntries)
        StaleEntries.Sweep(_readyAt, (_, at) => at <= DateTime.UtcNow);
    }

    /// <summary>
    /// The refusal these commands whisper, counting DOWN the seconds still to
    /// wait: [5/5] the instant you retry, [1/5] with under a second to go.
    /// </summary>
    public string Refusal(int secondsLeft) => $"Cooldown [{secondsLeft}/{Seconds}]";
}

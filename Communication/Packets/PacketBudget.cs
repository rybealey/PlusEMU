using System.Runtime.CompilerServices;
using Plus.HabboHotel.GameClients;

namespace Plus.Communication.Packets;

/// <summary>
/// pixelrp: one connection's packet budget - a token bucket per packet type
/// and one for everything it sends together (PacketLimits), and a count of
/// what went over.
///
/// A packet over either bucket is dropped. Nothing the client does comes near
/// the limits, so drops alone are only noise from a packet tool; what is acted
/// on is a flood - <see cref="FloodDrops"/> dropped packets inside
/// <see cref="FloodWindowMs"/>, far past any human, which the PacketManager
/// answers by writing it down and disconnecting.
///
/// Kept beside the connection rather than on it, and gone with it.
/// </summary>
internal sealed class PacketBudget
{
    public const int FloodWindowMs = 10_000;
    public const int FloodDrops = 100;

    private static readonly ConditionalWeakTable<GameClient, PacketBudget> Budgets = new();

    public static PacketBudget For(GameClient session) => Budgets.GetValue(session, _ => new PacketBudget());

    private readonly object _lock = new();
    private readonly Dictionary<Type, (double Tokens, long At)> _buckets = new();
    private readonly Dictionary<Type, int> _dropsByType = new();
    private double _totalTokens = PacketLimits.Total.Burst;
    private long _totalAt = Environment.TickCount64;
    private long _windowStart = Environment.TickCount64;
    private int _drops;
    private bool _flooded;

    /// <summary>
    /// Takes one packet of <paramref name="type"/> from the budget - from its own
    /// bucket and the total. False when either is empty and the packet must be
    /// dropped; <paramref name="flooded"/> is then true exactly once - the drop
    /// that tipped the connection into a flood.
    /// </summary>
    public bool TryTake(Type type, PacketLimit limit, out bool flooded)
    {
        flooded = false;
        lock (_lock)
        {
            var now = Environment.TickCount64;
            var bucket = _buckets.TryGetValue(type, out var held) ? held : (limit.Burst, now);
            var tokens = Math.Min(limit.Burst, bucket.Tokens + (now - bucket.At) * limit.PerSecond / 1000.0);
            var total = Math.Min(PacketLimits.Total.Burst, _totalTokens + (now - _totalAt) * PacketLimits.Total.PerSecond / 1000.0);
            _totalAt = now;
            if (tokens >= 1 && total >= 1)
            {
                _buckets[type] = (tokens - 1, now);
                _totalTokens = total - 1;
                return true;
            }
            _buckets[type] = (tokens, now);
            _totalTokens = total;

            if (now - _windowStart >= FloodWindowMs)
            {
                _windowStart = now;
                _drops = 0;
                _dropsByType.Clear();
            }
            _drops++;
            _dropsByType[type] = _dropsByType.GetValueOrDefault(type) + 1;
            if (_drops >= FloodDrops && !_flooded)
            {
                _flooded = true;
                flooded = true;
            }
            return false;
        }
    }

    /// <summary>The packet type dropped most in the current window, and how many of all were.</summary>
    public (string Packet, int PacketDrops, int Drops) Worst()
    {
        lock (_lock)
        {
            var worst = _dropsByType.OrderByDescending(entry => entry.Value).FirstOrDefault();
            return (worst.Key?.Name ?? "", worst.Value, _drops);
        }
    }
}

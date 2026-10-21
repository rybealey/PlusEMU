namespace Plus.Communication.Attributes;

/// <summary>
/// pixelrp: how often one connection may send this packet - a token bucket
/// holding <see cref="Burst"/> at most, refilled at <see cref="PerSecond"/>.
/// A packet over the budget is dropped at the PacketManager before any handler
/// runs (PacketBudget); a connection that keeps going far over it is flooding
/// and is disconnected and written down (PacketFloodLog).
///
/// Packets without the attribute get <see cref="Default"/>, far above anything
/// the client sends. The attribute is for handlers that cost the server more
/// than a packet costs a packet tool to send, such as a catalog scan. Walking
/// is the movement system's and is never budgeted (PacketManager).
/// </summary>
[AttributeUsage(AttributeTargets.Class)]
public class RateLimitAttribute : Attribute
{
    public static readonly RateLimitAttribute Default = new(30, 150);

    public RateLimitAttribute(double perSecond, int burst)
    {
        PerSecond = perSecond;
        Burst = burst;
    }

    public double PerSecond { get; }

    public int Burst { get; }
}

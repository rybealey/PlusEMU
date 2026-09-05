using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Rooms.Movement;

namespace Plus.Communication.Packets.Outgoing.Rooms.Engine;

/// <summary>
/// pixelrp Movement V2 - packet 4110. THE movement wire contract.
///
/// Replaces 3955 outright. 3955 is retired and NEVER reused: a V1 client fed a
/// V2 payload on 3955 would not reject it, it would parse field 0 as virtualId
/// and drive a fabricated avatar. A brand new header is the only fail-safe
/// option, because an unregistered header is a silent no-op on the client
/// (SocketConnection.getMessagesForWrapper logs UNREGISTERED and returns), so a
/// version skew degrades to native rendering instead of garbage coordinates.
///
/// formatVersion is field 0 so a future V2-vs-V2 skew is discarded whole rather
/// than mis-parsed.
///
/// Timing is ABSOLUTE: cycleStart travels as a signed delta against the
/// packet's own serverNow sample, both on the server's monotonic movement
/// clock. The client reconstructs cycleStart and renders
/// position = lerp(edge, (estServerNow - cycleStart) / interval), so a late
/// packet still describes an edge whose start time is already correct. That is
/// the whole point: V1's native path restarts its interpolation window from
/// PACKET ARRIVAL, which turns arrival jitter into position jitter.
/// </summary>
public class RpMovementV2Composer : IServerPacket
{
    public const int FormatVersion = 2;

    // flags
    public const int FlagEdge = 0x0001;
    public const int FlagWalkEnd = 0x0002;
    public const int FlagDisplacement = 0x0004;
    public const int FlagFinalEdge = 0x0020;
    public const int FlagCorrection = 0x0040;

    private readonly MovementEdgeRecord _edge;

    public uint MessageId => ServerPacketHeader.RpMovementV2Composer;

    public RpMovementV2Composer(MovementEdgeRecord edge) => _edge = edge;

    public void Compose(IOutgoingPacket packet)
    {
        // serverNow is sampled at compose time so the client's clock-offset
        // estimate reflects real send latency; cycleStart rides as a small
        // signed delta because the monotonic tick does not fit an int.
        var now = _edge.ServerNowMs;

        packet.WriteInteger(FormatVersion);          // 0
        packet.WriteInteger(_edge.VirtualId);        // 1
        packet.WriteInteger(_edge.Flags);            // 2
        packet.WriteInteger((int)(_edge.WalkSessionId & 0x7fffffff)); // 3
        packet.WriteInteger(_edge.RouteRevision);    // 4
        packet.WriteInteger(_edge.EdgeIndex);        // 5
        packet.WriteInteger(0);                      // 6 timingGroupId (unused)
        packet.WriteInteger(_edge.IntervalMs);       // 7
        packet.WriteInteger((int)(_edge.CycleStartMs - now)); // 8 signed delta
        packet.WriteInteger((int)(now >> 32));       // 9
        packet.WriteInteger((int)(now & 0xffffffff));// 10
        packet.WriteInteger(_edge.FromX);            // 11
        packet.WriteInteger(_edge.FromY);            // 12
        packet.WriteInteger(_edge.FromZ100);         // 13
        packet.WriteInteger(_edge.ToX);              // 14
        packet.WriteInteger(_edge.ToY);              // 15
        packet.WriteInteger(_edge.ToZ100);           // 16

        // Provisional lookahead: the walker's next real route tiles, so the
        // client can queue future edges and never wait at a tile boundary.
        // From-tile, timing and index are all derivable from the chain.
        var count = _edge.LookaheadCount;
        packet.WriteInteger(count);                  // 17
        for (var i = 0; i < count; i++)
        {
            packet.WriteInteger(_edge.Lookahead[i].X);
            packet.WriteInteger(_edge.Lookahead[i].Y);
            packet.WriteInteger(_edge.Lookahead[i].Z100);
        }
    }
}

using Plus.HabboHotel.GameClients;

namespace Plus.Communication.Packets.Outgoing.Users;

/// <summary>
/// pixelrp: why this connection is about to be closed, sent right before it
/// is. The socket closing looks the same to the client whatever the cause, so
/// without this every drop reads as "Connection lost" - including the ones the
/// player caused (a second tab, an hour idle) and the ones we caused (a
/// restart). The client hands the reason to the website's /game page, which
/// shows the right words for each.
/// </summary>
public class RpDisconnectReasonComposer : IServerPacket
{
    /// <summary>The same account connected from somewhere else.</summary>
    public const int ReasonReplaced = 1;
    /// <summary>An hour without walking, talking or acting.</summary>
    public const int ReasonIdle = 2;
    /// <summary>The server is going down for a restart.</summary>
    public const int ReasonRestart = 3;

    private readonly int _reason;
    private readonly string _detail;

    public uint MessageId => ServerPacketHeader.RpDisconnectReasonComposer;

    public RpDisconnectReasonComposer(int reason, string detail = "")
    {
        _reason = reason;
        _detail = detail;
    }

    public void Compose(IOutgoingPacket packet)
    {
        packet.WriteInteger(_reason);
        packet.WriteString(_detail);
    }
}

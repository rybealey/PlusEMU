using Plus.HabboHotel.GameClients;

namespace Plus.Communication.Packets.Outgoing.Corporations;

/// <summary>
/// pixelrp Corporation Panel: the answer to a change made in the window (issue
/// or revoke a warning or strike) - which action, whether it worked, and the
/// sentence to show: what happened or why not.
/// </summary>
public class RpCorpPanelResultComposer : IServerPacket
{
    private readonly int _action;
    private readonly bool _ok;
    private readonly string _message;

    public uint MessageId => ServerPacketHeader.RpCorpPanelResultComposer;

    public RpCorpPanelResultComposer(int action, bool ok, string message)
    {
        _action = action;
        _ok = ok;
        _message = message ?? "";
    }

    public void Compose(IOutgoingPacket packet)
    {
        packet.WriteInteger(_action);
        packet.WriteBoolean(_ok);
        packet.WriteString(_message);
    }
}

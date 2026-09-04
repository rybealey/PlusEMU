using Plus.HabboHotel.GameClients;

namespace Plus.Communication.Packets.Outgoing.Rooms.Chat;

/// <summary>
/// pixelrp: tells the sender's chat box to keep a command prefix (":ga",
/// ":ca") for their next message. Sent only when the command actually went
/// through, so a refused alert (off duty, no gang) leaves the box clear.
/// </summary>
public class RpRetainChatPrefixComposer : IServerPacket
{
    private readonly string _prefix;

    public uint MessageId => ServerPacketHeader.RpRetainChatPrefixComposer;

    public RpRetainChatPrefixComposer(string prefix)
    {
        _prefix = prefix;
    }

    public void Compose(IOutgoingPacket packet)
    {
        packet.WriteString(_prefix ?? "");
    }
}

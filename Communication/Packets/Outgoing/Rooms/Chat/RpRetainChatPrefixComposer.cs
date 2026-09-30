using Plus.HabboHotel.GameClients;

namespace Plus.Communication.Packets.Outgoing.Rooms.Chat;

/// <summary>
/// pixelrp: the answer to an alert (:ga, :ca, :sa) about the prefix the
/// sender's chat box kept for their next message.
///
/// The client puts the prefix back the moment the alert is SENT, without
/// waiting for this - waiting was a round trip long, and anything typed in
/// that gap went out as room chat. So the prefix is the confirmation, and
/// <see cref="Drop"/> (an empty prefix) the correction: the alert was refused
/// (no gang, off duty, not staff), and a box still holding just the prefix is
/// cleared. A client from before the correction ignores an empty one.
/// </summary>
public class RpRetainChatPrefixComposer : IServerPacket
{
    /// <summary>The alert was refused: the prefix the client kept goes.</summary>
    public static RpRetainChatPrefixComposer Drop() => new(string.Empty);

    /// <summary>The commands whose prefix the chat box keeps - see the client's ChatInputView.</summary>
    public static bool Retains(string commandKey) =>
        commandKey is "ga" or "ca" or "sa";

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

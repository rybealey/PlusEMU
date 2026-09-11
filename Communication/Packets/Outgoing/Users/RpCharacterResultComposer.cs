using Plus.HabboHotel.GameClients;

namespace Plus.Communication.Packets.Outgoing.Users;

/// <summary>
/// pixelrp: the answer to a create or a switch.
///
/// Three outcomes, and the client needs to tell them apart: it worked, it was
/// refused with a reason to show on the form, or it worked AND the client has
/// to reload to become somebody else. The reload is its own outcome because
/// nothing else in the hotel asks the client to leave.
/// </summary>
public class RpCharacterResultComposer : IServerPacket
{
    public const int Created = 0;
    public const int Refused = 1;
    public const int Reload = 2;

    private readonly int _outcome;
    private readonly string _message;

    public uint MessageId => ServerPacketHeader.RpCharacterResultComposer;

    public RpCharacterResultComposer(int outcome, string message = "")
    {
        _outcome = outcome;
        _message = message ?? "";
    }

    public void Compose(IOutgoingPacket packet)
    {
        packet.WriteInteger(_outcome);
        packet.WriteString(_message);
    }
}

using Plus.HabboHotel.Users.Banking;

namespace Plus.Communication.Packets.Outgoing.Users.Banking;

/// <summary>
/// pixelrp: why a banking request was refused.
///
/// The message is composed on the SERVER, following RpCharacterResultComposer:
/// the client knows the code so it can style the answer, but the wording lives
/// in one place, which is the only way "only 4,200c fits before your savings
/// is full" can be said at all - the client does not know the ceiling maths.
/// </summary>
public class RpBankResultComposer : IServerPacket
{
    private readonly BankResult _result;
    private readonly string _message;

    public uint MessageId => ServerPacketHeader.RpBankResultComposer;

    public RpBankResultComposer(BankResult result, string message)
    {
        _result = result;
        _message = message ?? string.Empty;
    }

    public void Compose(IOutgoingPacket packet)
    {
        packet.WriteInteger((int)_result);
        packet.WriteString(_message);
    }
}

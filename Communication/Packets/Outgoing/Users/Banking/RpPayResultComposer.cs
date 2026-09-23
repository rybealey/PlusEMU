using Plus.HabboHotel.GameClients;

namespace Plus.Communication.Packets.Outgoing.Users.Banking;

/// <summary>
/// pixelrp Pixel Cash: whether the payment went, and what to say if it did not.
///
/// The sentence comes from the server, the same rule the rest of the bank
/// already follows (BankResult's own comment): a refusal reads the same at the
/// ATM, in Mercury and here, and there is one place to change the wording.
///
/// It is also what closes the sheet. The sheet must not close on the tap -
/// that is how a player ends up believing money moved when it did not.
/// </summary>
public class RpPayResultComposer : IServerPacket
{
    private readonly bool _ok;
    private readonly string _message;
    private readonly long _remainingToday;

    public uint MessageId => ServerPacketHeader.RpPayResultComposer;

    public RpPayResultComposer(bool ok, string message, long remainingToday)
    {
        _ok = ok;
        _message = message ?? string.Empty;
        _remainingToday = remainingToday;
    }

    public void Compose(IOutgoingPacket packet)
    {
        packet.WriteInteger(_ok ? 1 : 0);
        packet.WriteString(_message);
        packet.WriteInteger(Plus.HabboHotel.Users.Banking.BankUtility.ToWire(_remainingToday));
    }
}

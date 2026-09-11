using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Users.Banking;

namespace Plus.Communication.Packets.Outgoing.Users.Banking;

/// <summary>
/// pixelrp: open the ATM screen, sent when a player uses an ATM furni.
///
/// The ATM is SERVER-OPENED rather than a window the client can summon: the
/// player has to be standing at a machine, and the only thing that knows that
/// is the room. Nothing the client sends can make this appear.
///
/// It carries the current account and the cash in hand, and NOT savings.
/// Savings is reachable only through the Wallet - that is what gives the two
/// accounts different characters, one you can reach from the floor and one you
/// have to sit down with - so the ATM is never even told the number.
/// </summary>
public class RpAtmOpenComposer : IServerPacket
{
    private readonly BankAccount? _account;
    private readonly int _cash;

    public uint MessageId => ServerPacketHeader.RpAtmOpenComposer;

    public RpAtmOpenComposer(BankAccount? account, int cash)
    {
        _account = account;
        _cash = cash;
    }

    public void Compose(IOutgoingPacket packet)
    {
        packet.WriteInteger(_account == null ? 0 : 1);
        packet.WriteInteger(BankUtility.ToWire(_account?.Current ?? 0));
        packet.WriteInteger(_cash < 0 ? 0 : _cash);
    }
}

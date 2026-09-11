using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Users.Banking;

namespace Plus.Communication.Packets.Outgoing.Users.Banking;

/// <summary>
/// pixelrp: this character's two accounts, for the Wallet's debit card.
///
/// Always the whole screen, never a delta - the RpPrivacyComposer convention.
/// A character with no account is sent the same packet with hasAccount 0 and
/// zeroes, so the client has one message to render both the "open an account"
/// state and the account itself, and there is no way for the two to disagree.
///
/// Only ever the recipient's own accounts. Nothing here carries a user id,
/// because there is no case where a player is shown somebody else's balance.
/// </summary>
public class RpBankAccountsComposer : IServerPacket
{
    private readonly BankAccount? _account;

    public uint MessageId => ServerPacketHeader.RpBankAccountsComposer;

    public RpBankAccountsComposer(BankAccount? account)
    {
        _account = account;
    }

    public void Compose(IOutgoingPacket packet)
    {
        packet.WriteInteger(_account == null ? 0 : 1);
        packet.WriteInteger(BankUtility.ToWire(_account?.Current ?? 0));
        packet.WriteInteger(BankUtility.ToWire(_account?.Savings ?? 0));
        packet.WriteInteger(BankUtility.ToWire(BankUtility.SavingsCap));
        packet.WriteInteger(BankUtility.SavingsRateBps);
        // Seconds still to run before the next interest payment, so the card
        // can show progress toward it rather than a number that jumps.
        packet.WriteInteger(Math.Max(0, BankUtility.InterestPeriodSeconds - (_account?.SavingsSeconds ?? 0)));
        packet.WriteInteger(BankUtility.ToWire(_account?.InterestTotal ?? 0));
        packet.WriteInteger(BankUtility.ToWire(_account?.WagesTotal ?? 0));
    }
}

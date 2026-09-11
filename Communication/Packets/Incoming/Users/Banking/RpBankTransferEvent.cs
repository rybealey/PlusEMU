using Plus.Communication.Packets.Outgoing.Users.Banking;
using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Users.Banking;

namespace Plus.Communication.Packets.Incoming.Users.Banking;

/// <summary>
/// pixelrp: move money between this character's own two accounts.
///
/// There is no target user id in this packet. Transfers are always between the
/// sender's own accounts, so there is nothing to forge - which is a stronger
/// ownership check than any amount of validation on a field that exists.
///
/// Direction must be exactly 0 or 1; it is compared, never cast, so a value
/// off the end of the enum cannot index anything.
/// </summary>
internal class RpBankTransferEvent : IPacketEvent
{
    private const int ToSavings = 0;
    private const int ToCurrent = 1;

    public Task Parse(GameClient session, IIncomingPacket packet)
    {
        var habbo = session.GetHabbo();
        if (habbo == null)
            return Task.CompletedTask;

        var direction = packet.ReadInt();
        var amount = packet.ReadInt();

        if (direction != ToSavings && direction != ToCurrent)
        {
            session.Send(new RpBankResultComposer(BankResult.InvalidAmount, "That transfer could not be completed."));
            return Task.CompletedTask;
        }

        var from = direction == ToSavings ? BankAccountKind.Current : BankAccountKind.Savings;
        var result = BankUtility.Transfer(habbo.Id, habbo.Username, from, amount, out var account, out var message);

        if (result == BankResult.Ok)
        {
            session.Send(new RpBankAccountsComposer(account));
            return Task.CompletedTask;
        }

        // The balances go with the refusal: whatever the client believed was
        // wrong enough to refuse it, so the screen is corrected at the same
        // time it is told why.
        session.Send(new RpBankAccountsComposer(account ?? BankUtility.Get(habbo.Id)));
        session.Send(new RpBankResultComposer(result, message));
        return Task.CompletedTask;
    }
}

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

        // A refusal sends the REASON and nothing else. The accounts push is
        // what tells the screen a transfer went through - it clears the amount
        // and the last message - so sending one here would wipe the entry the
        // player is about to correct.
        //
        // The exception is a character whose row has gone: their whole picture
        // is wrong, not just this amount.
        if (result == BankResult.NoAccount)
            session.Send(new RpBankAccountsComposer(null));
        session.Send(new RpBankResultComposer(result, message));
        return Task.CompletedTask;
    }
}

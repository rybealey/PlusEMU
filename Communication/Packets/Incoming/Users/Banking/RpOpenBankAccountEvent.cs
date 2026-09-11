using Plus.Communication.Packets.Outgoing.Users.Banking;
using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Users.Banking;

namespace Plus.Communication.Packets.Incoming.Users.Banking;

/// <summary>
/// pixelrp: open a current account and a savings account.
///
/// No payload. The two accounts are opened together and there is no variant to
/// name, and a field the server ignores is a field somebody later trusts.
///
/// Idempotent on the primary key, so a double click or a retried packet
/// cannot make a second account or zero an existing one.
/// </summary>
internal class RpOpenBankAccountEvent : IPacketEvent
{
    public Task Parse(GameClient session, IIncomingPacket packet)
    {
        var habbo = session.GetHabbo();
        if (habbo == null)
            return Task.CompletedTask;

        var result = BankUtility.Open(habbo.Id, habbo.Username, out var account);

        if (result == BankResult.Ok)
        {
            session.Send(new RpBankAccountsComposer(account));
            // Said once, at the moment the player chooses it, rather than on
            // every payday: wages stop arriving in hand from here on, and the
            // ATM is how they come back out.
            session.SendWhisper("Your accounts are open. Wages are paid into your current account from now on - use an ATM to take cash out.");
            return Task.CompletedTask;
        }

        if (result == BankResult.AlreadyOpen)
        {
            session.Send(new RpBankAccountsComposer(account));
            return Task.CompletedTask;
        }

        session.Send(new RpBankResultComposer(result, "Your accounts could not be opened."));
        return Task.CompletedTask;
    }
}

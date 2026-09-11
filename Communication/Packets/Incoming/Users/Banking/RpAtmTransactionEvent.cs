using Plus.Communication.Packets.Outgoing.Inventory.Purse;
using Plus.Communication.Packets.Outgoing.Users.Banking;
using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Users.Banking;

namespace Plus.Communication.Packets.Incoming.Users.Banking;

/// <summary>
/// pixelrp: a deposit or a withdrawal at an ATM.
///
/// Gated on AtmSessions: the window is opened by the server when the player
/// uses the furni, and this refuses unless that is still true and they are
/// still in the room the machine is in. Without the gate these two packets are
/// a bank the player carries around with them.
///
/// Only ever the CURRENT account. The ATM is never told what is in savings and
/// has no way to name it.
/// </summary>
internal class RpAtmTransactionEvent : IPacketEvent
{
    private const int Deposit = 0;
    private const int Withdraw = 1;

    public Task Parse(GameClient session, IIncomingPacket packet)
    {
        var habbo = session.GetHabbo();
        if (habbo == null)
            return Task.CompletedTask;

        var mode = packet.ReadInt();
        var amount = packet.ReadInt();

        if (mode != Deposit && mode != Withdraw)
            return Task.CompletedTask;

        if (!AtmSessions.IsAtMachine(habbo))
        {
            session.Send(new RpBankResultComposer(BankResult.Failed, "You are not at a cash machine."));
            return Task.CompletedTask;
        }

        var source = $"ATM in room {habbo.CurrentRoom?.Id ?? 0}";
        BankAccount? account;
        string message;
        BankResult result;

        if (mode == Deposit)
            result = BankUtility.Deposit(habbo, amount, source, out account, out message);
        else
            result = BankUtility.Withdraw(habbo, amount, source, out account, out message);

        if (result != BankResult.Ok)
        {
            // Just the reason. An RpAtmOpenComposer is what takes the screen
            // back to the balances, so sending one on a refusal would throw
            // away the amount the player typed and hide the message behind a
            // screen change.
            session.Send(new RpBankResultComposer(result, message));
            return Task.CompletedTask;
        }

        // The purse moved, so the HUD has to be told: the ATM is the only
        // place bank money and hand money meet, and a stale purse here is the
        // one that looks like the machine ate it.
        session.Send(new CreditBalanceComposer(habbo.Credits));
        session.Send(new RpAtmOpenComposer(account, habbo.Credits));
        session.Send(new RpBankAccountsComposer(account));
        return Task.CompletedTask;
    }
}

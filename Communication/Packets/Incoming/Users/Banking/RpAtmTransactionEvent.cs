using Plus.Communication.Packets.Outgoing.Inventory.Purse;
using Plus.Communication.Packets.Outgoing.Rooms.Chat;
using Plus.Communication.Packets.Outgoing.Users.Banking;
using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Users;
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
/// Only ever CHECKING. The ATM is never told what is in savings and
/// has no way to name it.
///
/// Both movements announce themselves to the room, amount included. Standing
/// at a machine counting out cash is a PUBLIC act - it is what makes a payday
/// worth following someone home for - and a bank that moved money silently
/// would quietly delete that whole piece of play. It is also the only record
/// anyone in the room has, since neither balance is visible to them.
/// </summary>
internal class RpAtmTransactionEvent : IPacketEvent
{
    private const int Deposit = 0;
    private const int Withdraw = 1;

    /// <summary>
    /// The blue action bubble the rest of the RP commands use - fighting,
    /// police, consuming. An ATM withdrawal is an action somebody performs in
    /// the room, so it reads in the same voice as the others rather than
    /// inventing a style of its own.
    /// </summary>
    private const int ActionBubble = 4;

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
            session.Send(new RpBankResultComposer(BankResult.Failed, "You are not at an ATM."));
            return Task.CompletedTask;
        }

        // Named, not numbered. A room id means nothing to the person
        // reading their own ledger a week later; the room they stood in
        // does. Trimmed to fit `source`, which is varchar(96).
        var roomName = habbo.CurrentRoom?.Name;
        var source = string.IsNullOrEmpty(roomName)
            ? "ATM"
            : $"ATM at {(roomName.Length > 70 ? roomName.Substring(0, 70) : roomName)}";
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

        Announce(habbo, mode, amount);

        // The purse moved, so the HUD has to be told: the ATM is the only
        // place bank money and hand money meet, and a stale purse here is the
        // one that looks like the machine ate it.
        session.Send(new CreditBalanceComposer(habbo.Credits));
        session.Send(new RpAtmOpenComposer(account, habbo.Credits));
        session.Send(new RpBankAccountsComposer(account));
        return Task.CompletedTask;
    }

    /// <summary>
    /// Says out loud what just happened, to everybody in the room.
    ///
    /// Third person and starred, the same shape every other RP action uses, so
    /// it reads as something the character did rather than as a notification
    /// that happens to be sitting over their head.
    ///
    /// Silent if the player has somehow left the room between the transaction
    /// and this - the money has already moved and is not worth unwinding over
    /// a bubble nobody would have seen anyway.
    /// </summary>
    private static void Announce(Habbo habbo, int mode, int amount)
    {
        var room = habbo.CurrentRoom;
        var user = room?.GetRoomUserManager()?.GetRoomUserByHabbo(habbo.Id);
        if (user == null)
            return;

        var message = (mode == Deposit)
            ? $"*deposits {amount:N0}c into the bank*"
            : $"*withdraws {amount:N0}c from the bank*";

        room.SendPacket(new ChatComposer(user.VirtualId, message, 0, ActionBubble));
    }
}

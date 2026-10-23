using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Users.Banking;
using Plus.Utilities;

namespace Plus.HabboHotel.Rooms.Chat.Commands.User;

/// <summary>
/// pixelrp: :bal - what is in your bank account, as a whisper (Twist,
/// 2026-10-23). The bank account is savings.
///
/// Reads the snapshot the bank keeps from login (BankUtility.Get), not
/// EnsureLoaded: that one restamps the savings interest clock, and asking what
/// you have must not cost you the interest you were part-way to earning.
/// </summary>
internal class BalanceCommand : IChatCommand
{
    public string Key => "bal";
    public string PermissionRequired => "command_bal";

    public string Parameters => "";

    public string Description => "See how much is in your bank account.";

    public void Execute(GameClient session, Room room, string[] parameters)
    {
        var account = BankUtility.Get(session.GetHabbo().Id);
        if (account == null)
        {
            session.SendWhisper("You do not have a bank account. Head to Mercury Bank to open one.");
            return;
        }

        // Savings is "the bank account" (Twist, 2026-10-23); checking stays in
        // the Wallet.
        session.SendWhisper($"You have {TextHandling.GetMoney(account.Savings)} in your bank account.");
    }
}

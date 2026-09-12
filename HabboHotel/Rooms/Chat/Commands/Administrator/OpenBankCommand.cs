using Plus.Communication.Packets.Outgoing.Users.Banking;
using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Users.Banking;

namespace Plus.HabboHotel.Rooms.Chat.Commands.Administrator;

/// <summary>
/// pixelrp: opens a player's bank accounts.
///
/// Accounts used to be opened from the Wallet. They are opened at a BANK now,
/// in person - and the branch is not built yet, so without this nothing
/// downstream of holding an account can be reached at all.
///
/// A bridge, not a feature. Once the branch exists this stays as the support
/// tool for the player a teller could not help.
///
/// Opening is idempotent, so running it twice tells the caller the accounts
/// were already there rather than resetting anything.
/// </summary>
internal class OpenBankCommand : IChatCommand
{
    public string Key => "openbank";
    public string PermissionRequired => "command_openbank";

    public string Parameters => "%username%";

    public string Description => "Open a player's bank accounts.";

    public void Execute(GameClient session, Room room, string[] parameters)
    {
        if (parameters.Length == 0)
        {
            session.SendWhisper("Who for? :openbank <username>");
            return;
        }

        var target = PlusEnvironment.Game.ClientManager.GetClientByUsername(parameters[0]);
        var habbo = target?.GetHabbo();
        if (habbo == null)
        {
            session.SendWhisper($"{parameters[0]} is not online.");
            return;
        }

        var result = BankUtility.Open(habbo.Id, habbo.Username, out var account);

        if (result == BankResult.AlreadyOpen)
        {
            session.SendWhisper($"{habbo.Username} already holds an account.");
            return;
        }

        if (result != BankResult.Ok)
        {
            session.SendWhisper($"Could not open accounts for {habbo.Username}.");
            return;
        }

        target.Send(new RpBankAccountsComposer(account));
        target.SendWhisper("Your accounts are open. Wages are paid into your checking account from now on - use an ATM to take cash out.");
        session.SendWhisper($"Opened accounts for {habbo.Username}.");
    }
}

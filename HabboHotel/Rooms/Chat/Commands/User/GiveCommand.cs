using Plus.Communication.Packets.Outgoing.Inventory.Purse;
using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Users;
using Plus.Utilities;

namespace Plus.HabboHotel.Rooms.Chat.Commands.User;

/// <summary>
/// pixelrp: hand another player money out of your own purse.
///
/// This used to be the staff command that minted currency from nothing, and
/// it did not work: the command manager strips the username before calling a
/// target command, so its reads of parameters[1] and [2] were one slot past
/// the currency and the amount, and every invocation answered "'10' is not a
/// valid currency". Staff who still need to create money have the RCON
/// give_user_currency command, which is where minting belongs anyway.
///
/// THE PURSE ONLY. Bank balances live in rp_bank_accounts and are never
/// touched here - the bank is reached through a teller or an ATM, and money in
/// hand is what changes hands in a room. Habbo.Credits is that money in hand,
/// and the logout save writes it back absolutely.
///
/// CASH ONLY, and no currency to name: :give &lt;username&gt; &lt;amount&gt;
/// (Twist, 2026-10-18). It used to take "cash" or "diamonds"; diamonds are the
/// bought currency and no longer change hands.
///
/// Which means both sides must be adjusted in the same breath, and they are:
/// the debit and the credit sit together with nothing between them that can
/// fail. The residual hazard is a server crash between the transfer and the
/// two logouts, which would persist whichever save happened to run - the same
/// hazard every credit change in this emulator carries, including buying
/// furniture, and not one worth inventing a transaction log for here.
/// </summary>
internal class GiveCommand : ITargetChatCommand
{
    /// <summary>
    /// The most that can change hands in one go.
    ///
    /// A ceiling on the SINGLE transfer, which is what stops a fat-fingered
    /// zero emptying somebody's purse into a stranger's. It is deliberately
    /// not a daily total: repeating the command still moves any amount, and
    /// pretending otherwise would be security theatre. If handing over large
    /// sums needs to be genuinely bounded, that wants a per-day ledger.
    /// </summary>
    private const int MaxCash = 10000;

    public string Key => "give";
    public string PermissionRequired => "command_give";

    public string Parameters => "%username% %amount%";

    public string Description => "Hand another player money from your purse.";

    /// <summary>Money passes hand to hand, so both hands must be present.</summary>
    public bool MustBeInSameRoom => true;

    public Task Execute(GameClient session, Room room, Habbo target, string[] parameters)
    {
        var habbo = session.GetHabbo();
        if (habbo == null || target == null)
            return Task.CompletedTask;

        // The manager has already taken the username off the front, so what is
        // left is the amount.
        if (parameters.Length < 1)
        {
            session.SendWhisper("Give how much? :give <username> <amount>");
            return Task.CompletedTask;
        }

        if (target.Id == habbo.Id)
        {
            session.SendWhisper("You cannot hand money to yourself.");
            return Task.CompletedTask;
        }

        if (!int.TryParse(parameters[0], out var amount) || amount <= 0)
        {
            session.SendWhisper("That is not an amount. Whole numbers above zero only: :give <username> <amount>");
            return Task.CompletedTask;
        }

        if (amount > MaxCash)
        {
            session.SendWhisper($"You can hand over at most {TextHandling.GetMoney(MaxCash)} at a time.");
            return Task.CompletedTask;
        }
        if (habbo.Credits < amount)
        {
            session.SendWhisper($"You only have {TextHandling.GetMoney(habbo.Credits)} on you.");
            return Task.CompletedTask;
        }
        // Nobody can hold more than an int, and a purse that wrapped round to
        // negative would be a far worse bug than a refusal.
        if (target.Credits > int.MaxValue - amount)
        {
            session.SendWhisper($"{target.Username} cannot carry that much.");
            return Task.CompletedTask;
        }

        habbo.Credits -= amount;
        target.Credits += amount;
        Plus.HabboHotel.Users.Banking.CoinLedger.Record(habbo, -amount, $"Gave to {target.Username}"); Plus.HabboHotel.Users.Banking.CoinLedger.Record(target, amount, $"Given by {habbo.Username}");
        session.Send(new CreditBalanceComposer(habbo.Credits));
        target.Client?.Send(new CreditBalanceComposer(target.Credits));
        Announce(room, habbo, target, TextHandling.GetMoney(amount));
        return Task.CompletedTask;
    }

    /// <summary>
    /// Bubble 5, shouted, wrapped in asterisks.
    ///
    /// Leading AND trailing "*" matter: the client only reads a bubble as an
    /// action when the text is wrapped in them, and it then moves the opening
    /// marker ahead of the speaker's name - so this renders as
    /// "*Yavn hands twist $7,500*" rather than repeating the name. The sum
    /// arrives already written (TextHandling.GetMoney).
    /// </summary>
    private static void Announce(Room room, Habbo giver, Habbo target, string sum)
    {
        var giverUser = room?.GetRoomUserManager()?.GetRoomUserByHabbo(giver.Id);

        if (giverUser != null)
            giverUser.OnChat(5, $"*hands {target.Username} {sum}*", true);
        else
            giver.Client?.SendWhisper($"You hand {target.Username} {sum}.");
    }
}

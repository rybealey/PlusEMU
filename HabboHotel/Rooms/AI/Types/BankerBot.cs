using Plus.Communication.Packets.Outgoing.Inventory.Purse;
using Plus.Communication.Packets.Outgoing.Users.Banking;
using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Users;
using Plus.HabboHotel.Users.Banking;

namespace Plus.HabboHotel.Rooms.AI.Types;

/// <summary>
/// pixelrp: the bank teller.
///
/// Everything a player can do with money happens through furniture or a phone
/// app - the ATM handles cash, Mercury handles transfers. This is the counter:
/// click the teller, pick deposit or withdraw, and answer two questions out
/// loud. The bank lobby stops being scenery.
///
/// IT OWNS NO BANKING LOGIC. Every amount, every ceiling, every refusal comes
/// from BankUtility, which already validates all of it and writes its own
/// wording - the teller's whole job is to ask, to parse, and to say the answer
/// back. A second place that decides whether you can afford something is a
/// second place that can disagree with the first.
///
/// SAVINGS IS COMPOSED, NOT SPECIAL-CASED. BankUtility.Withdraw reaches
/// checking only, by design, so taking cash out of savings is Transfer then
/// Withdraw - which is also what makes a savings withdrawal cost one of the
/// three weekly moves, because Transfer spends the allowance in the same
/// UPDATE that moves the money. The teller does not count transfers itself and
/// must never learn how.
///
/// The conversation lives here rather than in a static registry because a
/// conversation belongs to ONE teller: walking to a different counter should
/// not pick up a half-finished sentence. Keyed by user id, so one teller can
/// serve a queue and two players never see each other's prompts.
/// </summary>
internal class BankerBot : BotAi
{
    /// <summary>How far a player may stand and still be served. Manhattan, so
    /// this is the two rings of tiles around the counter, diagonals
    /// included.</summary>
    private const int ServiceRange = 2;

    /// <summary>Bot speech bubble. 2 is the bot bubble the rest of the
    /// emulator uses for anything a bot says.</summary>
    private const int TellerBubble = 2;

    /// <summary>Ticks - the room cycle runs one per second - before an
    /// unanswered question is dropped.</summary>
    private const int PatienceTicks = 60;

    /// <summary>A number nobody legitimately types, past which the answer is a
    /// mistake or a probe rather than an amount. int.MaxValue is the real
    /// ceiling: the purse is a 32-bit integer.</summary>
    private const long AbsurdAmount = int.MaxValue;

    private enum Step
    {
        /// <summary>Asked checking or savings.</summary>
        Account,
        /// <summary>Asked how much.</summary>
        Amount
    }

    private sealed class Conversation
    {
        public Step Step;
        public bool Withdrawing;
        public BankAccountKind Account;
        /// <summary>Ticks left before the teller gives up.</summary>
        public int Patience = PatienceTicks;
        /// <summary>One misunderstanding is a typo; two is a player who is not
        /// talking to the teller.</summary>
        public int Misses;
    }

    private readonly int _virtualId;
    private readonly Dictionary<int, Conversation> _talking = new();

    public BankerBot(int virtualId)
    {
        _virtualId = virtualId;
    }

    public override void OnSelfEnterRoom() { }

    public override void OnSelfLeaveRoom(bool kicked) => _talking.Clear();

    public override void OnUserEnterRoom(RoomUser user) { }

    public override void OnUserLeaveRoom(GameClient client)
    {
        // Nothing to say - they are gone. Dropping the record is the point, so
        // that returning starts a fresh conversation rather than answering a
        // question asked before they left.
        var habbo = client?.GetHabbo();

        if (habbo != null)
            _talking.Remove(habbo.Id);
    }

    public override void OnUserSay(RoomUser user, string message) => Hear(user, message);

    // Shouting is a different packet and a different fan-out (RoomUser.cs), so
    // without this a player who shouts an amount is simply ignored.
    public override void OnUserShout(RoomUser user, string message) => Hear(user, message);

    public override void OnTimerTick()
    {
        if (_talking.Count == 0)
            return;

        foreach (var (userId, conversation) in _talking.ToList())
        {
            var user = GetRoom()?.GetRoomUserManager()?.GetRoomUserByHabbo(userId);

            if (user == null)
            {
                _talking.Remove(userId);
                continue;
            }

            // Noticed on the tick as well as on the next reply, so somebody who
            // wanders off mid-sentence is told rather than left with a teller
            // that has silently stopped listening.
            if (!InRange(user))
            {
                _talking.Remove(userId);
                Say($"I'll be here when you're ready, {Name(user)}.");
                continue;
            }

            if (--conversation.Patience > 0)
                continue;

            _talking.Remove(userId);
            Say($"No trouble, {Name(user)}. Come back when you've decided.");
        }
    }

    /// <summary>
    /// A menu click. Called by RpTellerActionEvent, which has already checked
    /// that this bot is the one the player clicked and that they are in range -
    /// this re-checks the range anyway, because between the packet and here the
    /// player may have taken a step.
    /// </summary>
    public void Serve(GameClient session, TellerAction action)
    {
        var habbo = session?.GetHabbo();
        var user = habbo == null ? null : GetRoom()?.GetRoomUserManager()?.GetRoomUserByHabbo(habbo.Id);

        if (user == null)
            return;

        if (!InRange(user))
        {
            Say($"Step up to the counter and I'll help you, {Name(user)}.");
            return;
        }

        if (action == TellerAction.OpenAccount)
        {
            OpenAccount(session, habbo, user);
            return;
        }

        if (!BankUtility.HasAccount(habbo.Id))
        {
            Say($"You'll need an account with us first, {Name(user)}. I can open one now.");
            return;
        }

        // A second click replaces the player's own pending question rather than
        // queueing behind it: they have just told us what they actually want.
        _talking[habbo.Id] = new Conversation
        {
            Step = Step.Account,
            Withdrawing = action == TellerAction.Withdraw
        };

        Say(action == TellerAction.Withdraw
            ? $"Certainly, {Name(user)}. Withdrawing from checking or savings?"
            : $"Of course, {Name(user)}. Into checking or savings?");
    }

    private void OpenAccount(GameClient session, Habbo habbo, RoomUser user)
    {
        var result = BankUtility.Open(habbo.Id, habbo.Username, out var account);

        if (result == BankResult.AlreadyOpen)
        {
            Say($"You already hold an account with us, {Name(user)}.");
            return;
        }

        if (result != BankResult.Ok || account == null)
        {
            Say("I'm sorry - I can't open an account just now. Do try again shortly.");
            return;
        }

        session.Send(new RpBankAccountsComposer(account));
        Say($"Welcome to Mercury, {Name(user)}. Your checking and savings accounts are open, and your wages will be paid in from now on.");
    }

    private void Hear(RoomUser user, string message)
    {
        var habbo = user?.GetClient()?.GetHabbo();

        if (habbo == null || !_talking.TryGetValue(habbo.Id, out var conversation))
            return;

        // Range first: it is the cheapest test, and every bot in the room runs
        // this method for every line anybody says.
        if (!InRange(user))
        {
            _talking.Remove(habbo.Id);
            Say($"I'll be here when you're ready, {Name(user)}.");
            return;
        }

        conversation.Patience = PatienceTicks;

        if (conversation.Step == Step.Account)
            HearAccount(user, habbo, conversation, message);
        else
            HearAmount(user, habbo, conversation, message);
    }

    private void HearAccount(RoomUser user, Habbo habbo, Conversation conversation, string message)
    {
        var word = message.Trim().ToLowerInvariant();

        // "c" and "s" as well as the words: this is a chat line, not a form,
        // and a teller who insists on the full word is a teller people stop
        // using.
        if (word.StartsWith("check") || word == "c" || word == "current")
            conversation.Account = BankAccountKind.Current;
        else if (word.StartsWith("saving") || word == "s")
            conversation.Account = BankAccountKind.Savings;
        else
        {
            Miss(user, habbo, conversation, "Checking or savings?");
            return;
        }

        conversation.Step = Step.Amount;
        conversation.Misses = 0;

        var where = conversation.Account == BankAccountKind.Savings ? "savings" : "checking";

        Say(conversation.Withdrawing
            ? $"How much would you like to withdraw from {where}?"
            : $"How much would you like to deposit into {where}?");
    }

    private void HearAmount(RoomUser user, Habbo habbo, Conversation conversation, string message)
    {
        // Digits only, taken from anywhere in the line, so "500c" and "about
        // 500" both work. A minus sign is deliberately not read: a negative
        // deposit is a withdrawal by another name, and BankUtility would refuse
        // it anyway - better to ask again than to look like it nearly worked.
        var digits = new string(message.Where(char.IsDigit).ToArray());

        if (digits.Length == 0 || !long.TryParse(digits, out var amount) || amount <= 0)
        {
            Miss(user, habbo, conversation, "How much, exactly?");
            return;
        }

        if (amount > AbsurdAmount)
        {
            Say("I'm afraid that's rather more than this branch handles.");
            _talking.Remove(habbo.Id);
            return;
        }

        _talking.Remove(habbo.Id);

        var session = user.GetClient();
        var source = Source();

        var (result, refusal, account) = conversation.Withdrawing
            ? Withdraw(habbo, conversation.Account, amount, source)
            : Deposit(habbo, conversation.Account, amount, source);

        if (result != BankResult.Ok || account == null)
        {
            // BankUtility's own wording, spoken rather than whispered. It is
            // already written as a sentence a person says.
            Say(string.IsNullOrEmpty(refusal)
                ? "I'm sorry - that didn't go through."
                : refusal);
            return;
        }

        // The purse moved, so the HUD has to be told - the same reason the ATM
        // pushes it. Mercury and the Wallet read the accounts push.
        session.Send(new CreditBalanceComposer(habbo.Credits));
        session.Send(new RpBankAccountsComposer(account));

        var where = conversation.Account == BankAccountKind.Savings ? "savings" : "checking";

        Say(conversation.Withdrawing
            ? $"{amount:N0}c from {where}. Thank you, {Name(user)}."
            : $"{amount:N0}c into {where}. Thank you, {Name(user)}.");
    }

    /// <summary>
    /// Cash in. Checking is one call; savings is a deposit followed by a move
    /// across, because cash only ever enters at checking.
    /// </summary>
    private static (BankResult, string, BankAccount?) Deposit(Habbo habbo, BankAccountKind into, long amount, string source)
    {
        var result = BankUtility.Deposit(habbo, amount, source, out var account, out var message);

        if (result != BankResult.Ok || into == BankAccountKind.Current)
            return (result, message, account);

        // Paying INTO savings never touches the weekly allowance; the only
        // thing that can refuse here is the savings ceiling, and Transfer says
        // how much still fits. The cash is already in checking at that point,
        // which is the honest outcome - it is in the bank, just not in the
        // account they named.
        var moved = BankUtility.Transfer(habbo.Id, habbo.Username, BankAccountKind.Current, amount,
            out var after, out var moveMessage);

        return moved != BankResult.Ok
            ? (moved, $"{moveMessage} It's in your checking account for now.", after ?? account)
            : (BankResult.Ok, string.Empty, after);
    }

    /// <summary>
    /// Cash out. Savings goes through checking, which is what makes it spend
    /// one of the three weekly transfers - Transfer decrements the allowance in
    /// the same UPDATE that moves the money, so there is no second counter to
    /// keep in step.
    /// </summary>
    private static (BankResult, string, BankAccount?) Withdraw(Habbo habbo, BankAccountKind from, long amount, string source)
    {
        if (from == BankAccountKind.Savings)
        {
            var moved = BankUtility.Transfer(habbo.Id, habbo.Username, BankAccountKind.Savings, amount,
                out var after, out var moveMessage);

            if (moved != BankResult.Ok)
                return (moved, moveMessage, after);
        }

        var result = BankUtility.Withdraw(habbo, amount, source, out var account, out var message);

        return (result, message, account);
    }

    /// <summary>
    /// Where the money was handled, for the ledger. The same shape the ATM
    /// writes ("ATM at {room}") - and the prefix the Mercury app keys on to
    /// label the row Teller Deposit rather than Cash Deposit.
    /// </summary>
    private string Source()
    {
        var name = GetRoom()?.Name;

        return string.IsNullOrEmpty(name) ? "Teller" : $"Teller at {Truncate(name, 60)}";
    }

    private void Miss(RoomUser user, Habbo habbo, Conversation conversation, string retry)
    {
        if (++conversation.Misses < 2)
        {
            Say(retry);
            return;
        }

        _talking.Remove(habbo.Id);
        Say($"Not to worry, {Name(user)}. Give me a shout when you're ready.");
    }

    private bool InRange(RoomUser user)
    {
        var self = GetRoomUser();

        return self != null && user != null &&
               Gamemap.TileDistance(self.X, self.Y, user.X, user.Y) <= ServiceRange;
    }

    private void Say(string message) => GetRoomUser()?.Chat(message, TellerBubble);

    private static string Name(RoomUser user) => user?.GetClient()?.GetHabbo()?.Username ?? "there";

    private static string Truncate(string value, int length) =>
        value.Length <= length ? value : value[..length];
}

/// <summary>What the player picked from the teller's menu.</summary>
public enum TellerAction
{
    OpenAccount = 0,
    Deposit = 1,
    Withdraw = 2
}

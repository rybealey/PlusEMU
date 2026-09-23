using Dapper;
using Plus.Communication.Packets.Outgoing.Users.Banking;
using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Users.Accounts;
using Plus.Utilities;

namespace Plus.HabboHotel.Users.Banking;

/// <summary>
/// pixelrp: Pixel Cash - paying another player from inside a conversation.
///
/// THE RECIPIENT IS NEVER NAMED BY THE CLIENT. The packet carries a user id,
/// but every rule below is re-checked here against the database, so the id
/// buys nothing a forged one could not already reach: you cannot pay somebody
/// with no account, you cannot pay yourself, and you cannot pay another
/// character on your own account. That last is the one that matters most -
/// without it, a player could shuffle money between their own characters and
/// walk straight around the savings ration.
///
/// THE CARD IS DRAWN FROM rp_pay_transfers, NOT FROM A MESSAGE. See
/// 152_PixelCash.sql for why: a receipt that travels as text is a receipt
/// anybody can type.
///
/// Static, like BankUtility, whose locks and ledger do the actual money.
/// </summary>
public static class PixelCash
{
    /// <summary>The smallest payment. Below this it is a gesture, not a transfer.</summary>
    public const int Minimum = 10;

    /// <summary>The most one payment may carry.</summary>
    public const int Maximum = 10000;

    /// <summary>
    /// The most one character may send in a day, every recipient together.
    ///
    /// Summed from the rows rather than counted anywhere, because a counter
    /// has to be reset by something and whatever resets it is the thing that
    /// breaks. The day is UTC, which is the clock every other timestamp in
    /// this codebase already keeps.
    /// </summary>
    public const int DailyMaximum = 25000;

    /// <summary>Note column width. Kept in step with the varchar.</summary>
    public const int NoteWidth = 64;

    /// <summary>How many payments a conversation shows. Older ones stay in the table.</summary>
    public const int HistoryLimit = 50;

    /// <summary>Why the menu item is not offered, which is not the same as why a send failed.</summary>
    public enum Eligibility
    {
        /// <summary>Both banked, different accounts: show it.</summary>
        Ok = 0,
        /// <summary>The sender has no account. Show it disabled - that is theirs to fix.</summary>
        NoSenderAccount = 1,
        /// <summary>
        /// The recipient has no account, or is a character on the sender's own
        /// account. HIDE it: in the first case the reason is somebody else's
        /// business, and in the second there is nothing to explain.
        /// </summary>
        Unavailable = 2
    }

    /// <summary>One payment, as a conversation reads it.</summary>
    public class Record
    {
        public int Id { get; set; }
        public int SenderId { get; set; }
        public int RecipientId { get; set; }
        public int Amount { get; set; }
        public string Note { get; set; } = string.Empty;
        public int CreatedAt { get; set; }
    }

    private static int Now() => (int)DateTimeOffset.UtcNow.ToUnixTimeSeconds();

    private static int StartOfDay() =>
        (int)new DateTimeOffset(DateTime.UtcNow.Date, TimeSpan.Zero).ToUnixTimeSeconds();

    public static string CleanNote(string? raw)
    {
        var text = (raw ?? string.Empty).Replace('\r', ' ').Replace('\n', ' ').Trim();
        return text.Length > NoteWidth ? text.Substring(0, NoteWidth) : text;
    }

    /// <summary>
    /// Whether a bank account exists, asked of the DATABASE rather than of
    /// BankUtility's cache - the cache only holds players who are online, and
    /// the whole point here is a recipient who may not be.
    /// </summary>
    private static bool HasAccount(int userId)
    {
        if (userId <= 0)
            return false;
        using var connection = PlusEnvironment.DatabaseManager.Connection();
        return connection.ExecuteScalar<int>(
            "SELECT COUNT(*) FROM `rp_bank_accounts` WHERE `user_id` = @userId LIMIT 1",
            new { userId }) > 0;
    }

    public static string NameOf(int userId)
    {
        if (userId <= 0)
            return string.Empty;
        using var connection = PlusEnvironment.DatabaseManager.Connection();
        return connection.ExecuteScalar<string>(
            "SELECT `username` FROM `users` WHERE `id` = @userId LIMIT 1",
            new { userId }) ?? string.Empty;
    }

    /// <summary>Whether this pair may exchange money at all, and how to say so.</summary>
    public static Eligibility Check(int senderId, int recipientId)
    {
        if (senderId <= 0 || recipientId <= 0 || senderId == recipientId)
            return Eligibility.Unavailable;
        // Own-account first: it outranks everything, and it must read as
        // "unavailable" rather than as anything about accounts, because a
        // player already knows their own characters.
        if (AccountUtility.SameAccount(senderId, recipientId))
            return Eligibility.Unavailable;
        if (!HasAccount(senderId))
            return Eligibility.NoSenderAccount;
        if (!HasAccount(recipientId))
            return Eligibility.Unavailable;
        return Eligibility.Ok;
    }

    /// <summary>Sent by this character since midnight UTC, every recipient together.</summary>
    public static long SentToday(int senderId)
    {
        if (senderId <= 0)
            return 0;
        using var connection = PlusEnvironment.DatabaseManager.Connection();
        return connection.ExecuteScalar<long?>(
            "SELECT SUM(`amount`) FROM `rp_pay_transfers` WHERE `sender_id` = @senderId AND `created_at` >= @since",
            new { senderId, since = StartOfDay() }) ?? 0;
    }

    /// <summary>What this character may still send today.</summary>
    public static long RemainingToday(int senderId) => Math.Max(0, DailyMaximum - SentToday(senderId));

    /// <summary>Every payment between two characters, oldest first.</summary>
    public static List<Record> Between(int aId, int bId)
    {
        if (aId <= 0 || bId <= 0)
            return new List<Record>();
        using var connection = PlusEnvironment.DatabaseManager.Connection();
        return connection.Query<Record>(
            "SELECT `id` AS Id, `sender_id` AS SenderId, `recipient_id` AS RecipientId, " +
            "`amount` AS Amount, `note` AS Note, `created_at` AS CreatedAt " +
            "FROM `rp_pay_transfers` " +
            "WHERE (`sender_id` = @aId AND `recipient_id` = @bId) " +
            "   OR (`sender_id` = @bId AND `recipient_id` = @aId) " +
            "ORDER BY `id` DESC LIMIT @limit",
            new { aId, bId, limit = HistoryLimit })
            .Reverse().ToList();
    }

    /// <summary>
    /// Send the money, record the payment, and tell both ends.
    ///
    /// Everything the sheet checked is checked again here. The sheet's copy of
    /// the rules is a courtesy so a player is not told no after the fact; this
    /// is the one that decides.
    /// </summary>
    public static bool Send(GameClient session, int recipientId, long amount, string note, out string message, out Record? record)
    {
        message = string.Empty;
        record = null;
        var habbo = session?.GetHabbo();
        if (habbo == null)
            return false;

        switch (Check(habbo.Id, recipientId))
        {
            case Eligibility.NoSenderAccount:
                message = "Open an account with Mercury first.";
                return false;
            case Eligibility.Unavailable:
                message = "You cannot send money to them.";
                return false;
        }

        if (amount < Minimum)
        {
            message = $"The smallest payment is {TextHandling.GetMoney(Minimum)}.";
            return false;
        }
        if (amount > Maximum)
        {
            message = $"The most you can send at once is {TextHandling.GetMoney(Maximum)}.";
            return false;
        }
        var remaining = RemainingToday(habbo.Id);
        if (amount > remaining)
        {
            message = remaining <= 0
                ? $"You have sent your {TextHandling.GetMoney(DailyMaximum)} for today."
                : $"You can send {TextHandling.GetMoney(remaining)} more today.";
            return false;
        }

        var recipientName = NameOf(recipientId);
        var clean = CleanNote(note);
        var result = BankUtility.Pay(habbo.Id, habbo.Username, recipientId, recipientName, amount, clean,
            out var senderAccount, out message);
        if (result != BankResult.Ok)
            return false;

        var now = Now();
        int id;
        using (var connection = PlusEnvironment.DatabaseManager.Connection())
        {
            // One statement: LAST_INSERT_ID() is per-connection, and Dapper
            // opens and closes one around every command.
            id = connection.ExecuteScalar<int>(
                "INSERT INTO `rp_pay_transfers` (`sender_id`,`recipient_id`,`amount`,`note`,`created_at`) " +
                "VALUES (@senderId, @recipientId, @amount, @note, @now); SELECT LAST_INSERT_ID();",
                new { senderId = habbo.Id, recipientId, amount = (int)amount, note = clean, now });
        }

        record = new Record
        {
            Id = id,
            SenderId = habbo.Id,
            RecipientId = recipientId,
            Amount = (int)amount,
            Note = clean,
            CreatedAt = now
        };

        // The sender's own screens, refreshed from the account the move
        // returned rather than read again.
        session!.Send(new RpBankAccountsComposer(senderAccount));
        session.Send(new RpPayReceiptComposer(record));

        var recipient = PlusEnvironment.Game.ClientManager.GetClientByUserId(recipientId);
        if (recipient?.GetHabbo() != null)
        {
            recipient.Send(new RpBankAccountsComposer(BankUtility.EnsureLoaded(recipientId)));
            recipient.Send(new RpPayReceiptComposer(record));
            recipient.SendNotification($"{habbo.Username} sent you {TextHandling.GetMoney(amount)}." +
                (clean.Length > 0 ? $" \"{clean}\"" : string.Empty));
        }
        return true;
    }
}

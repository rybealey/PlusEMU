using System.Collections.Concurrent;
using System.Data;
using Dapper;
using Plus.Communication.Packets.Outgoing.Users.Banking;
using Plus.HabboHotel.GameClients;

namespace Plus.HabboHotel.Users.Banking;

/// <summary>
/// pixelrp: the bank behind the Wallet's debit card and the ATM furni.
///
/// A character has a current account and a savings account, opened together.
/// Wages direct-deposit into the current account, savings earns interest for
/// every hour the player is actually online, and the ATM is the only way cash
/// crosses between the bank and the money in hand.
///
/// Static, like ShiftManager and StockLedger, because its two hottest callers
/// are static themselves: the shift payout loop and the furni interactor the
/// item system constructs with `new`. A DI singleton would have to be reached
/// through a static accessor on PlusEnvironment anyway, which is the same
/// coupling with more ceremony.
///
/// THE ONE HAZARD WORTH REMEMBERING: `users`.`credits` is rewritten
/// absolutely at logout from the in-memory Habbo.Credits. Nothing here may
/// treat that column as storage. Bank balances live in rp_bank_accounts and
/// are only ever mutated relatively; the only writes to `credits` are the
/// crash hedges in Deposit/Withdraw, which always accompany a matching change
/// to Habbo.Credits so the logout save writes the same total back.
/// </summary>
public static class BankUtility
{
    /// <summary>
    /// The savings ceiling. Savings is the account that COMPOUNDS, so an
    /// uncapped balance accelerates away on its own; the current account only
    /// grows as fast as somebody works, so it needs no ceiling.
    ///
    /// At the shipped rank pay (15-27c per ten minutes) this is on the order
    /// of a thousand hours of work, which is a ceiling a dedicated player can
    /// see rather than one nobody ever meets.
    /// </summary>
    public const long SavingsCap = 100000;

    /// <summary>
    /// Interest per hour of ONLINE time, in basis points. 25 = 0.25%/hour,
    /// which doubles a balance in roughly 280 online hours.
    ///
    /// Basis points as an int rather than a decimal rate on purpose: a double
    /// parsed or printed under the wrong culture turns 0.25 into 25, and this
    /// is the number that mints money.
    /// </summary>
    public const int SavingsRateBps = 25;

    /// <summary>Online seconds that buy one interest payment.</summary>
    public const int InterestPeriodSeconds = 3600;

    /// <summary>How often accrual is stamped. Also the eviction interval.</summary>
    private const int TickSeconds = 60;

    /// <summary>
    /// The most online time one tick may claim. Twice the period, so a long
    /// GC pause or a skipped tick is recovered, while a restart or a week
    /// offline can claim at most two minutes. The bias is deliberately toward
    /// under-paying: this is a money supply.
    /// </summary>
    private const int MaxDeltaPerTick = TickSeconds * 2;

    // Aliased to the row class's property names. Dapper maps onto a mutable
    // class rather than straight onto the BankAccount record: the record is
    // positional, and positional records have bitten this codebase before
    // where a column's type and the parameter's did not line up exactly.
    private const string SelectColumns =
        "SELECT `user_id` AS UserId, `current_balance` AS CurrentBalance, `savings_balance` AS SavingsBalance, " +
        "`savings_seconds` AS SavingsSeconds, `interest_total` AS InterestTotal, `wages_total` AS WagesTotal " +
        "FROM `rp_bank_accounts` ";

    private const string SelectSql = SelectColumns + "WHERE `user_id` = @userId LIMIT 1";

    private sealed class BankRow
    {
        public int UserId { get; set; }
        public long CurrentBalance { get; set; }
        public long SavingsBalance { get; set; }
        public int SavingsSeconds { get; set; }
        public long InterestTotal { get; set; }
        public long WagesTotal { get; set; }

        public BankAccount ToAccount() =>
            new(UserId, CurrentBalance, SavingsBalance, SavingsSeconds, InterestTotal, WagesTotal);
    }

    // Mirror of the DB, never the source of truth. Every mutation writes the
    // row first and refreshes this from what the row actually says, so a
    // clamped or refused write can never leave the cache optimistic.
    private static readonly ConcurrentDictionary<int, BankAccount> Accounts = new();

    // One lock per character, so two packets from one session serialise
    // without a hot server-wide lock on the payout path.
    private static readonly ConcurrentDictionary<int, object> Locks = new();

    private static System.Threading.Timer? _timer;

    private static NLog.ILogger Log => NLog.LogManager.GetLogger("Plus.HabboHotel.Users.Banking.BankUtility");

    public static void Init()
    {
        // A minute after boot, then every minute - the StockLedger shape. The
        // first fire is late on purpose: at boot nobody has been online long
        // enough to have earned anything, and the delay keeps this off the
        // startup path entirely.
        _timer = new System.Threading.Timer(_ => Tick(), null, TickSeconds * 1000, TickSeconds * 1000);

        Log.Info("Banking: savings {0} bps/hour, ceiling {1}c.", SavingsRateBps, SavingsCap);
    }

    /// <summary>
    /// Cheap, in memory, never queries. This is what the ten minute payout
    /// loop asks, so it must stay a dictionary read.
    /// </summary>
    public static bool HasAccount(int userId) => Accounts.ContainsKey(userId);

    /// <summary>The cached snapshot, or null for a character with no account.</summary>
    public static BankAccount? Get(int userId) => Accounts.TryGetValue(userId, out var account) ? account : null;

    /// <summary>
    /// One query, once per session, at login - the EnsureRpStatsLoaded shape.
    /// Does NOT create: a character without a row simply has no account, and
    /// opening one is a thing the player does.
    ///
    /// Also restamps `last_interest_at`, so the first tick after a login
    /// claims nothing for the time the player was away.
    /// </summary>
    public static BankAccount? EnsureLoaded(int userId)
    {
        if (userId <= 0)
            return null;
        try
        {
            using var connection = PlusEnvironment.DatabaseManager.Connection();
            var account = Load(connection, userId);
            if (account == null)
            {
                Accounts.TryRemove(userId, out _);
                return null;
            }
            connection.Execute(
                "UPDATE `rp_bank_accounts` SET `last_interest_at` = @now WHERE `user_id` = @userId LIMIT 1",
                new { userId, now = Now });
            Accounts[userId] = account;
            return account;
        }
        catch (Exception e)
        {
            Log.Error("Loading bank accounts for {0} failed: {1}", userId, e.Message);
            return null;
        }
    }

    /// <summary>Drops a character's snapshot; they are gone or switching.</summary>
    public static void Unload(int userId) => Accounts.TryRemove(userId, out _);

    /// <summary>
    /// Opens both accounts at zero. Idempotent on the primary key, so a
    /// double click or a retried packet cannot make a second account or reset
    /// the balances of an existing one.
    /// </summary>
    public static BankResult Open(int userId, string username, out BankAccount? account)
    {
        account = null;
        if (userId <= 0)
            return BankResult.Failed;
        lock (LockFor(userId))
        {
            try
            {
                using var connection = PlusEnvironment.DatabaseManager.Connection();
                var now = Now;
                var rows = connection.Execute(
                    "INSERT IGNORE INTO `rp_bank_accounts` (`user_id`, `opened_at`, `last_interest_at`) " +
                    "VALUES (@userId, @now, @now)", new { userId, now });
                account = Load(connection, userId);
                if (account == null)
                    return BankResult.Failed;
                Accounts[userId] = account;
                if (rows == 0)
                    return BankResult.AlreadyOpen;
                LogMovement(connection, userId, username, BankTransactionKind.Open, BankAccountKind.Current, 0, 0, "accounts opened");
                return BankResult.Ok;
            }
            catch (Exception e)
            {
                Log.Error("Opening bank accounts for {0} failed: {1}", userId, e.Message);
                return BankResult.Failed;
            }
        }
    }

    /// <summary>
    /// Money entering the bank from payroll. There is no inverse on this
    /// path: cash only leaves the bank through Withdraw, which is the ATM.
    /// </summary>
    public static BankResult CreditWages(int userId, string username, long amount, string source, out BankAccount? account)
    {
        account = null;
        if (amount <= 0)
            return BankResult.InvalidAmount;
        lock (LockFor(userId))
        {
            try
            {
                using var connection = PlusEnvironment.DatabaseManager.Connection();
                var rows = connection.Execute(
                    "UPDATE `rp_bank_accounts` SET `current_balance` = `current_balance` + @amount, " +
                    "`wages_total` = `wages_total` + @amount WHERE `user_id` = @userId LIMIT 1",
                    new { userId, amount });
                if (rows == 0)
                    return BankResult.NoAccount;
                account = Refresh(connection, userId);
                if (account == null)
                    return BankResult.Failed;
                LogMovement(connection, userId, username, BankTransactionKind.Wages, BankAccountKind.Current,
                    amount, account.Current, source);
                return BankResult.Ok;
            }
            catch (Exception e)
            {
                Log.Error("Paying wages into {0}'s account failed: {1}", userId, e.Message);
                return BankResult.Failed;
            }
        }
    }

    /// <summary>
    /// Between this character's own two accounts, and nowhere else. There is
    /// deliberately no target id anywhere in this path - a transfer has no
    /// recipient to forge.
    /// </summary>
    public static BankResult Transfer(int userId, string username, BankAccountKind from, long amount,
        out BankAccount? account, out string message)
    {
        account = null;
        message = string.Empty;
        if (amount <= 0)
        {
            message = "Enter an amount to move.";
            return BankResult.InvalidAmount;
        }
        lock (LockFor(userId))
        {
            try
            {
                using var connection = PlusEnvironment.DatabaseManager.Connection();
                var current = Load(connection, userId);
                if (current == null)
                {
                    Accounts.TryRemove(userId, out _);
                    message = "You do not have a bank account.";
                    return BankResult.NoAccount;
                }
                Accounts[userId] = current;
                var available = from == BankAccountKind.Current ? current.Current : current.Savings;
                if (available < amount)
                {
                    account = current;
                    message = from == BankAccountKind.Current
                        ? "Your current account does not hold that much."
                        : "Your savings account does not hold that much.";
                    return BankResult.InsufficientFunds;
                }

                // The ceiling is checked here rather than left to the UPDATE
                // so the refusal can name the amount that WOULD fit. A player
                // who is told only "no" has to guess.
                if (from == BankAccountKind.Current)
                {
                    var room = SavingsCap - current.Savings;
                    if (room <= 0)
                    {
                        account = current;
                        message = $"Your savings account is full at {SavingsCap:N0}c.";
                        return BankResult.SavingsFull;
                    }
                    if (amount > room)
                    {
                        account = current;
                        message = $"Only {room:N0}c fits before your savings account reaches {SavingsCap:N0}c.";
                        return BankResult.SavingsFull;
                    }
                }

                var sql = from == BankAccountKind.Current
                    ? "UPDATE `rp_bank_accounts` SET `current_balance` = `current_balance` - @amount, " +
                      "`savings_balance` = `savings_balance` + @amount " +
                      "WHERE `user_id` = @userId AND `current_balance` >= @amount AND `savings_balance` + @amount <= @cap LIMIT 1"
                    : "UPDATE `rp_bank_accounts` SET `savings_balance` = `savings_balance` - @amount, " +
                      "`current_balance` = `current_balance` + @amount " +
                      "WHERE `user_id` = @userId AND `savings_balance` >= @amount LIMIT 1";
                var rows = connection.Execute(sql, new { userId, amount, cap = SavingsCap });
                if (rows == 0)
                {
                    account = current;
                    message = "That transfer could not be completed.";
                    return BankResult.InsufficientFunds;
                }
                account = Refresh(connection, userId);
                if (account == null)
                    return BankResult.Failed;
                var to = from == BankAccountKind.Current ? BankAccountKind.Savings : BankAccountKind.Current;
                var fromBalance = from == BankAccountKind.Current ? account.Current : account.Savings;
                var toBalance = to == BankAccountKind.Current ? account.Current : account.Savings;
                LogMovement(connection, userId, username, BankTransactionKind.TransferOut, from, -amount, fromBalance, "transfer");
                LogMovement(connection, userId, username, BankTransactionKind.TransferIn, to, amount, toBalance, "transfer");
                return BankResult.Ok;
            }
            catch (Exception e)
            {
                Log.Error("Transfer for {0} failed: {1}", userId, e.Message);
                message = "That transfer could not be completed.";
                return BankResult.Failed;
            }
        }
    }

    /// <summary>
    /// ATM: cash in hand into the current account.
    ///
    /// The hand is debited FIRST. If anything fails between the two halves
    /// the money is lost rather than duplicated, which is the only acceptable
    /// direction for the error to point.
    /// </summary>
    public static BankResult Deposit(Habbo habbo, long amount, string source, out BankAccount? account, out string message)
    {
        account = null;
        message = string.Empty;
        if (habbo == null)
            return BankResult.Failed;
        if (amount <= 0)
        {
            message = "Enter an amount to deposit.";
            return BankResult.InvalidAmount;
        }
        if (amount > habbo.Credits)
        {
            account = Get(habbo.Id);
            message = "You are not carrying that much.";
            return BankResult.InsufficientFunds;
        }
        lock (LockFor(habbo.Id))
        {
            try
            {
                using var connection = PlusEnvironment.DatabaseManager.Connection();
                if (Load(connection, habbo.Id) == null)
                {
                    Accounts.TryRemove(habbo.Id, out _);
                    message = "You do not have a bank account.";
                    return BankResult.NoAccount;
                }
                // Habbo.Credits is the authority; the users row is a crash
                // hedge that the logout save will rewrite from memory anyway.
                habbo.Credits -= (int)amount;
                PersistHandCredits(connection, habbo.Id, -amount);

                var rows = connection.Execute(
                    "UPDATE `rp_bank_accounts` SET `current_balance` = `current_balance` + @amount " +
                    "WHERE `user_id` = @userId LIMIT 1", new { userId = habbo.Id, amount });
                if (rows == 0)
                {
                    // Put it back: the bank never took it.
                    habbo.Credits += (int)amount;
                    PersistHandCredits(connection, habbo.Id, amount);
                    message = "You do not have a bank account.";
                    return BankResult.NoAccount;
                }
                account = Refresh(connection, habbo.Id);
                if (account == null)
                    return BankResult.Failed;
                LogMovement(connection, habbo.Id, habbo.Username, BankTransactionKind.Deposit,
                    BankAccountKind.Current, amount, account.Current, source);
                return BankResult.Ok;
            }
            catch (Exception e)
            {
                Log.Error("Deposit for {0} failed: {1}", habbo.Id, e.Message);
                message = "That deposit could not be completed.";
                return BankResult.Failed;
            }
        }
    }

    /// <summary>
    /// ATM: current account into cash in hand. Savings is not reachable from
    /// here, by design - the ATM only ever names the current account.
    ///
    /// The bank is debited first, under a guarded UPDATE, so the balance can
    /// never go negative and a failure loses rather than mints.
    /// </summary>
    public static BankResult Withdraw(Habbo habbo, long amount, string source, out BankAccount? account, out string message)
    {
        account = null;
        message = string.Empty;
        if (habbo == null)
            return BankResult.Failed;
        if (amount <= 0)
        {
            message = "Enter an amount to withdraw.";
            return BankResult.InvalidAmount;
        }
        // The hand is an int on the wire and in memory; a withdrawal that
        // would overflow it is refused rather than wrapped.
        if (habbo.Credits + amount > int.MaxValue)
        {
            account = Get(habbo.Id);
            message = "You are carrying too much to withdraw that.";
            return BankResult.InvalidAmount;
        }
        lock (LockFor(habbo.Id))
        {
            try
            {
                using var connection = PlusEnvironment.DatabaseManager.Connection();
                var current = Load(connection, habbo.Id);
                if (current == null)
                {
                    Accounts.TryRemove(habbo.Id, out _);
                    message = "You do not have a bank account.";
                    return BankResult.NoAccount;
                }
                Accounts[habbo.Id] = current;
                if (current.Current < amount)
                {
                    account = current;
                    message = "Your current account does not hold that much.";
                    return BankResult.InsufficientFunds;
                }
                var rows = connection.Execute(
                    "UPDATE `rp_bank_accounts` SET `current_balance` = `current_balance` - @amount " +
                    "WHERE `user_id` = @userId AND `current_balance` >= @amount LIMIT 1",
                    new { userId = habbo.Id, amount });
                if (rows == 0)
                {
                    account = current;
                    message = "Your current account does not hold that much.";
                    return BankResult.InsufficientFunds;
                }
                account = Refresh(connection, habbo.Id);
                habbo.Credits += (int)amount;
                PersistHandCredits(connection, habbo.Id, amount);
                LogMovement(connection, habbo.Id, habbo.Username, BankTransactionKind.Withdraw,
                    BankAccountKind.Current, -amount, account?.Current ?? 0, source);
                return BankResult.Ok;
            }
            catch (Exception e)
            {
                Log.Error("Withdrawal for {0} failed: {1}", habbo.Id, e.Message);
                message = "That withdrawal could not be completed.";
                return BankResult.Failed;
            }
        }
    }

    /// <summary>
    /// Balances are bigint in the database and 32-bit on the wire. Clamping
    /// here means a number too large to send arrives wrong rather than
    /// arriving negative, which is the difference between a display bug and
    /// an exploitable one.
    /// </summary>
    public static int ToWire(long balance) => balance <= 0 ? 0 : balance >= int.MaxValue ? int.MaxValue : (int)balance;

    private static int Now => (int)DateTimeOffset.UtcNow.ToUnixTimeSeconds();

    private static object LockFor(int userId) => Locks.GetOrAdd(userId, _ => new object());

    private static BankAccount? Load(IDbConnection connection, int userId) =>
        connection.QueryFirstOrDefault<BankRow>(SelectSql, new { userId })?.ToAccount();

    private static BankAccount? Refresh(IDbConnection connection, int userId)
    {
        var account = Load(connection, userId);
        if (account != null)
            Accounts[userId] = account;
        return account;
    }

    /// <summary>
    /// The crash hedge on the hand balance, exactly the shape ShiftManager
    /// uses: a RELATIVE update alongside the in-memory change, which the
    /// absolute logout save then writes the same total over.
    /// </summary>
    private static void PersistHandCredits(IDbConnection connection, int userId, long delta) =>
        connection.Execute("UPDATE `users` SET `credits` = `credits` + @delta WHERE `id` = @userId LIMIT 1",
            new { userId, delta });

    private static void LogMovement(IDbConnection connection, int userId, string username, string kind,
        BankAccountKind account, long amount, long balanceAfter, string source)
    {
        try
        {
            connection.Execute(
                "INSERT INTO `rp_bank_transactions` (`user_id`, `username`, `kind`, `account`, `amount`, `balance_after`, `source`, `created_at`) " +
                "VALUES (@userId, @username, @kind, @account, @amount, @balanceAfter, @source, @now)",
                new
                {
                    userId,
                    username = username ?? string.Empty,
                    kind,
                    account = account == BankAccountKind.Current ? "current" : "savings",
                    amount,
                    balanceAfter,
                    source = source ?? string.Empty,
                    now = Now
                });
        }
        catch (Exception e)
        {
            // A missing log row must never undo a balance change that already
            // committed - the money is right, the paperwork is not.
            Log.Error("Writing a bank log row for {0} failed: {1}", userId, e.Message);
        }
    }

    /// <summary>
    /// Accrual, once a minute.
    ///
    /// Two statements. The first stamps online time for everyone connected -
    /// one set-based UPDATE, the StockLedger shape. The second picks out only
    /// the accounts that crossed an hour boundary in THIS minute, which in
    /// steady state is roughly the player count divided by sixty because
    /// people log in at different times and their hours self-stagger.
    /// </summary>
    private static void Tick()
    {
        try
        {
            var clients = PlusEnvironment.Game?.ClientManager?.GetClients;
            if (clients == null)
                return;

            // Two groups, and both matter.
            //
            // ACTIVE is in a room and not asleep. Socketed is not enough - a
            // client parked on the hotel view overnight is not playing - and
            // neither is being in a room, because a character stood still for
            // five minutes is a tab somebody left open. RoomUser.IsAsleep is
            // exactly where the hotel already draws that line: it is what
            // dims the avatar, and ShiftManager.InterruptForIdle ends a SHIFT
            // on the same signal. Wages and interest stopping together is the
            // point - one idea of being away, not two.
            //
            // IDLE is everybody else with an account who is still connected.
            // They earn nothing, but their clock is still stamped below, so
            // coming back does not hand them the time they were away.
            var active = new List<GameClient>();
            var idleIds = new List<int>();
            foreach (var client in clients)
            {
                var habbo = client?.GetHabbo();
                if (habbo == null || !HasAccount(habbo.Id))
                    continue;

                var user = habbo.CurrentRoom?.GetRoomUserManager()?.GetRoomUserByHabbo(habbo.Id);
                if (user == null || user.IsAsleep)
                    idleIds.Add(habbo.Id);
                else
                    active.Add(client);
            }

            // Whoever is cached and no longer connected is dropped. Doing it
            // here rather than on disconnect keeps this out of the ordering
            // around Habbo.OnDisconnect, where ShiftManager's final payout
            // still needs HasAccount to answer true.
            var connectedIds = new HashSet<int>();
            foreach (var client in clients)
            {
                var habbo = client?.GetHabbo();
                if (habbo != null)
                    connectedIds.Add(habbo.Id);
            }
            foreach (var id in Accounts.Keys)
            {
                if (!connectedIds.Contains(id))
                    Accounts.TryRemove(id, out _);
            }

            if (active.Count == 0 && idleIds.Count == 0)
                return;

            var now = Now;
            var activeIds = active.Select(c => c.GetHabbo().Id).ToArray();

            using var connection = PlusEnvironment.DatabaseManager.Connection();

            if (activeIds.Length > 0)
                connection.Execute(
                    "UPDATE `rp_bank_accounts` SET " +
                    "`savings_seconds` = `savings_seconds` + LEAST(GREATEST(@now - `last_interest_at`, 0), @maxDelta), " +
                    "`last_interest_at` = @now WHERE `user_id` IN @ids",
                    new { now, maxDelta = MaxDeltaPerTick, ids = activeIds });

            // Idle accounts have their clock moved forward WITHOUT earning
            // anything. Skipping them entirely would let the gap since their
            // last active tick grow, and the clamp would then hand them two
            // minutes of credit for the first tick after they came back - a
            // player could bank a slow trickle by going away and returning.
            if (idleIds.Count > 0)
                connection.Execute(
                    "UPDATE `rp_bank_accounts` SET `last_interest_at` = @now WHERE `user_id` IN @ids",
                    new { now, ids = idleIds.ToArray() });

            // Every accruing account comes back, not just the ones that have
            // crossed an hour - because the CACHE has to be refreshed either
            // way. The Wallet asks for its accounts once a minute while it is
            // open and is answered from the cache, so a cache that only moved
            // when interest landed would leave the countdown frozen at whatever
            // it read at login and then jump an hour.
            //
            // Idle accounts are read back too, so the Wallet of somebody who
            // has gone quiet shows a countdown that has genuinely stopped
            // rather than one left over from whenever they last moved.
            //
            // One statement for every connected account, and the ones that have
            // earned something are filtered out of the result in memory rather
            // than by a second query.
            var rows = connection.Query<BankRow>(
                SelectColumns + "WHERE `user_id` IN @ids",
                new { ids = activeIds.Concat(idleIds).ToArray() }).Select(r => r.ToAccount()).ToList();

            // Read outside the per-account locks on purpose. A transfer that
            // commits in the gap between this SELECT and this write would be
            // rolled back IN THE CACHE for up to a minute - never in the
            // database, and the client has already been sent the right figures
            // by the transfer itself, so the worst case is one stale poll that
            // the next one corrects. Closing a microsecond window properly
            // would cost a query per online player per minute, forever.
            foreach (var row in rows)
                Accounts[row.UserId] = row;

            foreach (var row in rows)
            {
                if (row.SavingsSeconds < InterestPeriodSeconds)
                    continue;
                PayInterest(connection, row, active.FirstOrDefault(c => c.GetHabbo().Id == row.UserId));
            }
        }
        catch (Exception e)
        {
            Log.Error("Bank interest tick failed: {0}", e.Message);
        }
    }

    /// <summary>
    /// One hour's interest for one account.
    ///
    /// The hour is spent whether or not it earned anything, so a balance
    /// sitting at the ceiling does not bank hours that would all pay out at
    /// once the moment a withdrawal made room.
    ///
    /// Interest CLAMPS at the ceiling rather than pushing through it -
    /// otherwise the one account with a limit would also be the one account
    /// that raises itself above it.
    /// </summary>
    private static void PayInterest(IDbConnection connection, BankAccount row, GameClient? client)
    {
        var interest = row.Savings * SavingsRateBps / 10000;
        var room = SavingsCap - row.Savings;
        if (interest > room)
            interest = room;
        if (interest < 0)
            interest = 0;

        // Seconds and balance move in one statement, so a crash between them
        // is not possible: the hour is either spent and paid, or neither.
        connection.Execute(
            "UPDATE `rp_bank_accounts` SET `savings_balance` = `savings_balance` + @interest, " +
            "`interest_total` = `interest_total` + @interest, " +
            "`savings_seconds` = `savings_seconds` - @period WHERE `user_id` = @userId LIMIT 1",
            new { userId = row.UserId, interest, period = InterestPeriodSeconds });

        var account = Refresh(connection, row.UserId);
        if (interest > 0 && account != null)
        {
            LogMovement(connection, row.UserId, client?.GetHabbo()?.Username ?? string.Empty,
                BankTransactionKind.Interest, BankAccountKind.Savings, interest, account.Savings,
                $"{SavingsRateBps / 100m:0.00}%/hr on {row.Savings:N0}c");
        }
        if (client != null && account != null)
            client.Send(new RpBankAccountsComposer(account));
    }
}

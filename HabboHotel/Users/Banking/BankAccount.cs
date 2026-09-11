namespace Plus.HabboHotel.Users.Banking;

/// <summary>
/// pixelrp: a character's two accounts, as one snapshot.
///
/// The pair is always read and written together - they are opened together
/// and cannot exist apart - so splitting them into two objects would only
/// create a state where one is stale and the other is not.
/// </summary>
public sealed record BankAccount(
    int UserId,
    long Current,
    long Savings,
    // Online seconds banked toward the next interest hour.
    int SavingsSeconds,
    long InterestTotal,
    long WagesTotal);

/// <summary>Which of a character's two accounts an operation names.</summary>
public enum BankAccountKind
{
    Current = 0,
    Savings = 1
}

/// <summary>
/// The answer to an operation. The client renders the message the server
/// sends rather than composing its own, so a refusal reads the same
/// everywhere and there is one place to change the wording.
/// </summary>
public enum BankResult
{
    Ok = 0,
    NoAccount = 1,
    AlreadyOpen = 2,
    InvalidAmount = 3,
    InsufficientFunds = 4,
    SavingsFull = 5,
    Failed = 6
}

/// <summary>
/// `kind` values written to rp_bank_transactions. Constants rather than an
/// enum because the column is a string a human reads, and a value that stops
/// being produced must stay readable in rows that already carry it.
/// </summary>
public static class BankTransactionKind
{
    public const string Open = "open";
    public const string Wages = "wages";
    public const string Interest = "interest";
    public const string TransferIn = "transfer_in";
    public const string TransferOut = "transfer_out";
    public const string Deposit = "deposit";
    public const string Withdraw = "withdraw";
}

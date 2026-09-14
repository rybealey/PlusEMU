using System.Globalization;

namespace Plus.Utilities;

public static class TextHandling
{
    public static string GetString(double k) => k.ToString(CultureInfo.InvariantCulture);

    /// <summary>
    /// A whole number the way a player reads one: 7500 becomes "7,500".
    ///
    /// Invariant, like GetString above and for the same reason - the separator
    /// is the one thing about a number that changes with the machine's locale,
    /// and a server that rendered 7.500 or 7 500 would be showing a different
    /// figure to whoever read it. No decimals: these are counts of things.
    /// </summary>
    public static string GetNumber(long k) => k.ToString("N0", CultureInfo.InvariantCulture);

    /// <summary>
    /// A sum of money the way a player reads one: "7,500 coins", "1 coin".
    ///
    /// The hotel's money has exactly one name in front of a player. It used to
    /// be written as a "c" suffix in the bank and as "credits" in the purse,
    /// which read as two currencies to anybody who had not seen the code.
    /// </summary>
    public static string GetCoins(long k) => $"{GetNumber(k)} {((k == 1) ? "coin" : "coins")}";
}
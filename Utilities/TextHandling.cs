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
    /// A sum of money the way a player reads one: "$7,500".
    ///
    /// The hotel's money has exactly one name in front of a player. It has
    /// been a "c" suffix in the bank, "credits" in the purse and "coins"
    /// everywhere else, each of which read as a different currency to anybody
    /// who had not seen the code. It is dollars now, and this is the only
    /// place that says so - every screen and every bubble goes through here.
    ///
    /// No decimals: the hotel's money is whole dollars, and ".00" on every
    /// figure is noise on a screen the size of a phone.
    /// </summary>
    public static string GetMoney(long k) => $"${GetNumber(k)}";
}
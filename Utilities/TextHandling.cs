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
}
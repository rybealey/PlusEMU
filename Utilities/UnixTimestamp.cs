namespace Plus.Utilities;

internal static class UnixTimestamp
{
    /// <summary>
    /// The largest Unix timestamp <see cref="FromUnixTimestamp" /> can safely convert
    /// (DateTime.MaxValue, 9999-12-31, expressed in Unix seconds). Anything above this,
    /// or at/below 0, makes AddSeconds throw ArgumentOutOfRangeException.
    /// </summary>
    private const double MaxTimestamp = 253402300799d;

    /// <summary>
    /// Gets the current date time now in Unix Timestamp format.
    /// </summary>
    /// <returns>Unix Timestamp.</returns>
    public static double GetNow()
    {
        var ts = DateTime.Now - new DateTime(1970, 1, 1, 0, 0, 0);
        return ts.TotalSeconds;
    }

    /// <summary>
    /// Converts the Unix Timestamp to a DateTime object.
    /// </summary>
    /// <param name="timestamp">Unix Timestamp.</param>
    /// <returns>DateTime object.</returns>
    public static DateTime FromUnixTimestamp(double timestamp)
    {
        var dt = new DateTime(1970, 1, 1, 0, 0, 0, 0);
        dt = dt.AddSeconds(timestamp);
        return dt;
    }

    /// <summary>
    /// Whether a stored timestamp is safe to feed into <see cref="FromUnixTimestamp" />
    /// and everything built on top of it (ages, formatted dates). A timestamp of 0
    /// (unset/default) is treated as invalid too, so callers skip it rather than
    /// rendering the Unix epoch.
    /// </summary>
    public static bool IsValid(double timestamp) => timestamp > 0 && timestamp <= MaxTimestamp;

    /// <summary>
    /// A ticket's age in milliseconds, clamped to what the client's int field can carry.
    /// int.MaxValue milliseconds is ~24.9 days, and tickets now outlive the emulator
    /// (persisted across restarts), so an unclamped subtraction throws OverflowException
    /// inside Compose for anything older — which disconnects the session mid-login
    /// rather than showing a stale ticket. An invalid timestamp (see <see cref="IsValid" />)
    /// reports as age 0 instead of throwing out of <see cref="FromUnixTimestamp" />.
    /// </summary>
    public static int AgeInMilliseconds(double timestamp)
    {
        if (!IsValid(timestamp))
            return 0;
        var ms = (DateTime.Now - FromUnixTimestamp(timestamp)).TotalMilliseconds;
        return (int)Math.Clamp(ms, 0d, int.MaxValue);
    }
}

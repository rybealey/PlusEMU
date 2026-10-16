using System.Collections.Concurrent;
using Dapper;
using Plus.HabboHotel.GameClients;

namespace Plus.HabboHotel.Users.Security;

/// <summary>
/// pixelrp: the client's browser guard (BrowserGuard.ts) caught a player
/// opening developer tools, or playing from an automated browser, and is
/// sending them back to the website. It tells the server first, and this
/// writes it down (rp_browser_guard_log, migration 259) so staff can see who
/// keeps trying.
///
/// The report comes from the client, so it is a lead and never proof: a
/// player can block it, and a tampered client can send it for nothing - only
/// ever about itself, since the account is the connection's own. Nothing is
/// done to the account here. A tampered client could also send it in a loop,
/// so each player is written down <see cref="MaxPerWindow"/> times per
/// <see cref="WindowSeconds"/> at most.
/// </summary>
public static class BrowserGuardLog
{
    /// <summary>A DevTools shortcut: F12, Inspect, Console, view-source.</summary>
    public const int ReasonShortcut = 1;

    /// <summary>DevTools found already open, however it got there.</summary>
    public const int ReasonDevToolsOpen = 2;

    /// <summary>A browser driven by automation (navigator.webdriver).</summary>
    public const int ReasonAutomation = 3;

    public const int MaxDetailLength = 255;

    private const int MaxPerWindow = 5;
    private const int WindowSeconds = 600;

    /// <summary>Player id -> the window's start and how many were written in it.</summary>
    private static readonly ConcurrentDictionary<int, (long Start, int Count)> Written = new();

    public static void Record(GameClient session, int reason, string detail)
    {
        var habbo = session?.GetHabbo();
        if (habbo == null || reason is < ReasonShortcut or > ReasonAutomation)
            return;

        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var entry = Written.AddOrUpdate(habbo.Id,
            _ => (now, 1),
            (_, last) => now - last.Start >= WindowSeconds ? (now, 1) : (last.Start, last.Count + 1));
        if (entry.Count > MaxPerWindow)
            return;

        detail = (detail ?? "").Trim();
        if (detail.Length > MaxDetailLength)
            detail = detail[..MaxDetailLength];

        using (var connection = PlusEnvironment.DatabaseManager.Connection())
        {
            connection.Execute(
                "INSERT INTO `rp_browser_guard_log` (`user_id`, `username`, `reason`, `detail`, `created_at`) " +
                "VALUES (@userId, @username, @reason, @detail, UNIX_TIMESTAMP())",
                new { userId = habbo.Id, username = habbo.Username, reason, detail });
        }

        Console.WriteLine($"[BrowserGuard] {habbo.Username} (#{habbo.Id}) sent away: {ReasonName(reason)} - {detail}");
    }

    private static string ReasonName(int reason) => reason switch
    {
        ReasonShortcut => "DevTools shortcut",
        ReasonDevToolsOpen => "DevTools open",
        ReasonAutomation => "automated browser",
        _ => "unknown"
    };
}

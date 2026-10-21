using Dapper;
using Plus.Database;
using Plus.HabboHotel.Users.Accounts;
using Plus.HabboHotel.Users.Authentication;

namespace Plus.HabboHotel.Users.Security;

/// <summary>
/// pixelrp: the browser guard's pause, enforced at login too.
///
/// The CMS already refuses a paused account a game ticket and a fresh asset
/// pass (App\Support\AssetPause), but only on the pages that issue tickets
/// today - a ticket minted before the catch, or by any route added later,
/// still got in. So login asks the same question: a <see cref="BrowserGuardLog"/>
/// row for any character on the account within ASSET_GUARD_PAUSE_MINUTES
/// (30 by default, as the CMS), and the account below the unlimited tier
/// (Developers, AssetPass::UNLIMITED_RANK), and there is no login. The client
/// gets the ordinary failed-handshake screen; nothing says why.
/// </summary>
internal class BrowserGuardPauseTask : IAuthenticationTask
{
    /// <summary>The CMS's AssetPass::UNLIMITED_RANK - never paused.</summary>
    private const int UnlimitedRank = 6;

    private static readonly int PauseSeconds = Math.Max(60,
        (int.TryParse(Environment.GetEnvironmentVariable("ASSET_GUARD_PAUSE_MINUTES"), out var minutes) ? minutes : 30) * 60);

    private readonly IDatabase _database;

    public BrowserGuardPauseTask(IDatabase database)
    {
        _database = database;
    }

    public async Task<bool> CanLogin(int userId)
    {
        if (AccountUtility.AccountRank(userId) >= UnlimitedRank)
            return true;

        using var connection = _database.Connection();
        var caught = await connection.ExecuteScalarAsync<int?>(
            "SELECT 1 FROM `rp_browser_guard_log` g JOIN `users` u ON u.`id` = g.`user_id` " +
            "WHERE (u.`id` = @root OR u.`parent_id` = @root) AND g.`created_at` > UNIX_TIMESTAMP() - @seconds LIMIT 1",
            new { root = AccountUtility.RootOf(userId), seconds = PauseSeconds });
        return caught == null;
    }
}

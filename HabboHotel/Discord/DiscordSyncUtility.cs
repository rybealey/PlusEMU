using Dapper;

namespace Plus.HabboHotel.Discord;

/// <summary>
/// pixelrp: emulator side of Discord role sync. The emulator NEVER talks to
/// Discord - it only enqueues per-user sync work (login/logout/vip events)
/// into discord_sync_queue; the CMS scheduler drains the queue and makes
/// every Discord API call. Only linked users generate work.
/// </summary>
public static class DiscordSyncUtility
{
    public static void Enqueue(int userId, string reason)
    {
        try
        {
            using var connection = PlusEnvironment.DatabaseManager.Connection();
            connection.Execute(
                "INSERT INTO `discord_sync_queue` (`user_id`, `reason`, `created_at`) " +
                "SELECT `id`, @reason, UNIX_TIMESTAMP() FROM `users` WHERE `id` = @userId AND `discord_id` IS NOT NULL",
                new { userId, reason });
        }
        catch
        {
            // sync is best-effort; never let it break login/logout/redemption
        }
    }

    public static bool IsLinked(int userId)
    {
        using var connection = PlusEnvironment.DatabaseManager.Connection();
        return connection.ExecuteScalar<string>(
            "SELECT `discord_id` FROM `users` WHERE `id` = @userId LIMIT 1", new { userId }) != null;
    }
}

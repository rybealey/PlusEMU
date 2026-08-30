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

    /// <summary>
    /// Link state for the Settings page. DiscordId is null when unlinked.
    /// </summary>
    public sealed class DiscordLinkState
    {
        public string? DiscordId { get; set; }
        public int DiscordLinkedAt { get; set; }
    }

    public static DiscordLinkState GetLinkState(int userId)
    {
        using var connection = PlusEnvironment.DatabaseManager.Connection();
        return connection.QueryFirstOrDefault<DiscordLinkState>(
            "SELECT `discord_id` AS `DiscordId`, `discord_linked_at` AS `DiscordLinkedAt` " +
            "FROM `users` WHERE `id` = @userId LIMIT 1",
            new { userId }) ?? new DiscordLinkState();
    }

    /// <summary>
    /// Clears the link in-game and queues the Discord-side cleanup for the
    /// CMS scheduler. Both writes share one transaction: `discord:sweep`
    /// only reconciles users that are still linked, so a lost queue row
    /// would strand the player's roles forever.
    /// Returns false when nothing was linked.
    /// </summary>
    public static bool Unlink(int userId)
    {
        try
        {
            using var connection = PlusEnvironment.DatabaseManager.Connection();
            connection.Open();

            using var transaction = connection.BeginTransaction();

            var discordId = connection.ExecuteScalar<string>(
                "SELECT `discord_id` FROM `users` WHERE `id` = @userId LIMIT 1",
                new { userId }, transaction);

            if (string.IsNullOrEmpty(discordId))
            {
                transaction.Rollback();
                return false;
            }

            connection.Execute(
                "UPDATE `users` SET `discord_id` = NULL, `discord_linked_at` = 0 WHERE `id` = @userId",
                new { userId }, transaction);

            connection.Execute(
                "INSERT INTO `discord_sync_queue` (`user_id`, `discord_id`, `reason`, `created_at`) " +
                "VALUES (@userId, @discordId, 'unlink', UNIX_TIMESTAMP())",
                new { userId, discordId }, transaction);

            transaction.Commit();
            return true;
        }
        catch
        {
            // Never let a disconnect attempt take the session down; the
            // player sees the unchanged state and can retry.
            return false;
        }
    }
}

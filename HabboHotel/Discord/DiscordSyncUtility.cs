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
        try
        {
            using var connection = PlusEnvironment.DatabaseManager.Connection();
            return connection.QueryFirstOrDefault<DiscordLinkState>(
                "SELECT `discord_id` AS `DiscordId`, `discord_linked_at` AS `DiscordLinkedAt` " +
                "FROM `users` WHERE `id` = @userId LIMIT 1",
                new { userId }) ?? new DiscordLinkState();
        }
        catch
        {
            // This is on the page-open path, called straight from Parse with
            // no caller-side try/catch - a faulted Parse gets the session
            // disconnected. Never let a transient DB blip (or a hotel where
            // the discord_linked_at column migration hasn't run yet) kick
            // the player out of the game just for opening Settings. An
            // empty state reads as "not linked", which is the safe default.
            return new DiscordLinkState();
        }
    }

    /// <summary>
    /// Clears the link in-game and queues the Discord-side cleanup for the
    /// CMS scheduler, returning the resulting state in the same round trip -
    /// the caller must not follow up with a second, unguarded read of link
    /// state. PacketManager disconnects the session on a faulted Parse, so a
    /// DB blip on that second read would kick the player out of the game
    /// for clicking Disconnect.
    /// Both writes share one transaction: `discord:sweep` only reconciles
    /// users that are still linked, so a lost queue row would strand the
    /// player's roles forever.
    ///
    /// Returns the already-unlinked state on success or when nothing was
    /// linked to begin with; the pre-read (still-linked) state if the write
    /// failed after that row was read, since the clear may not have
    /// committed - it must never be misreported as unlinked; or null if the
    /// failure happened before any row could be read, meaning the state is
    /// genuinely unknown.
    /// </summary>
    public static DiscordLinkState? Unlink(int userId)
    {
        DiscordLinkState? current = null;
        try
        {
            using var connection = PlusEnvironment.DatabaseManager.Connection();
            connection.Open();

            using var transaction = connection.BeginTransaction();

            current = connection.QueryFirstOrDefault<DiscordLinkState>(
                "SELECT `discord_id` AS `DiscordId`, `discord_linked_at` AS `DiscordLinkedAt` " +
                "FROM `users` WHERE `id` = @userId LIMIT 1",
                new { userId }, transaction) ?? new DiscordLinkState();

            if (string.IsNullOrEmpty(current.DiscordId))
            {
                transaction.Rollback();
                return current;
            }

            connection.Execute(
                "UPDATE `users` SET `discord_id` = NULL, `discord_linked_at` = 0 WHERE `id` = @userId",
                new { userId }, transaction);

            connection.Execute(
                "INSERT INTO `discord_sync_queue` (`user_id`, `discord_id`, `reason`, `created_at`) " +
                "VALUES (@userId, @discordId, 'unlink', UNIX_TIMESTAMP())",
                new { userId, discordId = current.DiscordId }, transaction);

            transaction.Commit();
            return new DiscordLinkState();
        }
        catch
        {
            // Never let a disconnect attempt take the session down; report
            // the last truth we actually observed instead of guessing.
            return current;
        }
    }
}

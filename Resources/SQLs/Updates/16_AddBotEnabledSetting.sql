-- In-game Claude bot toggle: staff command :bot on / :bot off (group 5+,
-- Administrator and up) flips server_settings.bot.enabled; the bot process
-- polls the row and connects/disconnects to match. BOT_ENABLED in the
-- container env remains the hard override.
--
-- Idempotent: both inserts dedupe on their primary keys.

INSERT IGNORE INTO `server_settings` (`key`, `value`, `description`) VALUES
    ('bot.enabled', '1', 'Claude bot kill switch, toggled in-game with :bot on/off.');

INSERT IGNORE INTO `permissions_commands` (`command`, `group_id`, `subscription_id`) VALUES
    ('command_bot', 5, 0);

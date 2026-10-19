-- pixelrp: :hug, :kiss and :bite are back for every player (Twist, 2026-10-20).
--
-- 275 deleted their rows as unused, but they never were: SocialCommand builds
-- the permission name from the command's key (command_{Key}), so a search for
-- 'command_hug' found nothing. With no row, HasCommand is false for everyone,
-- so all three dropped out of the :commands list and were refused if typed.
--
-- Same rows as 75: group_id 1 = every player, no VIP route. Takes effect at
-- each player's next login.
--
-- Idempotent: an upsert.

INSERT INTO `permissions_commands` (`command`, `group_id`, `subscription_id`) VALUES
    ('command_hug', 1, 0),
    ('command_kiss', 1, 0),
    ('command_bite', 1, 0)
ON DUPLICATE KEY UPDATE `group_id` = VALUES(`group_id`), `subscription_id` = VALUES(`subscription_id`);

-- Should list the three rows, each at 1 / 0.
SELECT `command`, `group_id`, `subscription_id` FROM `permissions_commands`
WHERE `command` IN ('command_hug', 'command_kiss', 'command_bite');

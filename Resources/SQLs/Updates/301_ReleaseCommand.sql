-- pixelrp: :release lets a prisoner out of jail early, and an Administrator in
-- jail out themselves (Twist, 2026-10-22: ":Release for admin+ to release
-- prisoners out of jail. and they can self release too if they are in jail").
--
-- group_id is a MINIMUM rank: 5 = Administrator and every rank above it.
-- Takes effect when the emulator restarts, which the deploy does next.
--
-- Idempotent: an upsert.

INSERT INTO `permissions_commands` (`command`, `group_id`, `subscription_id`)
VALUES ('command_release', 5, 0)
ON DUPLICATE KEY UPDATE `group_id` = VALUES(`group_id`), `subscription_id` = VALUES(`subscription_id`);

-- Should list one row at 5 / 0.
SELECT `command`, `group_id`, `subscription_id` FROM `permissions_commands`
WHERE `command` = 'command_release';

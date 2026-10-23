-- pixelrp: :bal (2026-10-23). Whispers what is in your bank account (savings).
-- Every player - group 1, where :stats and the other player commands sit.
--
-- group_id is a MINIMUM rank: 1 = User and every rank above it.
-- Takes effect when the emulator restarts, which the deploy does next.
--
-- Idempotent: an upsert.

INSERT INTO `permissions_commands` (`command`, `group_id`, `subscription_id`)
VALUES ('command_bal', 1, 0)
ON DUPLICATE KEY UPDATE `group_id` = VALUES(`group_id`), `subscription_id` = VALUES(`subscription_id`);

-- Should list one row at 1 / 0.
SELECT `command`, `group_id`, `subscription_id` FROM `permissions_commands`
WHERE `command` = 'command_bal';

-- pixelrp: :invisible (2026-10-22). Out of sight of everyone who isn't
-- invisible too, and :invisible name brings someone in the room into it.
-- Trial Moderator and up - the same ranks as the mod tool.
--
-- group_id is a MINIMUM rank: 3 = Trial Moderator and every rank above it.
-- Takes effect when the emulator restarts, which the deploy does next.
--
-- Idempotent: an upsert.

INSERT INTO `permissions_commands` (`command`, `group_id`, `subscription_id`)
VALUES ('command_invisible', 3, 0)
ON DUPLICATE KEY UPDATE `group_id` = VALUES(`group_id`), `subscription_id` = VALUES(`subscription_id`);

-- Should list one row at 3 / 0.
SELECT `command`, `group_id`, `subscription_id` FROM `permissions_commands`
WHERE `command` = 'command_invisible';

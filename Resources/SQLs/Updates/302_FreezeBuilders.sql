-- pixelrp: Builders can :freeze and :unfreeze themselves (Twist, 2026-10-22:
-- "allow builders to be able to :Freeze and :unfreeze themselves. they cannot
-- use this command on other people"). Both were Trial Moderator and up (285);
-- FreezeCommand.Target keeps a Builder to themselves, and Trial Moderators
-- and up freeze anybody in the room as before.
--
-- group_id is a MINIMUM rank: 2 = Builder and every rank above it. Takes
-- effect when the emulator restarts, which the deploy does next.
--
-- Idempotent: an upsert.

INSERT INTO `permissions_commands` (`command`, `group_id`, `subscription_id`)
VALUES ('command_freeze', 2, 0), ('command_unfreeze', 2, 0)
ON DUPLICATE KEY UPDATE `group_id` = VALUES(`group_id`), `subscription_id` = VALUES(`subscription_id`);

-- Should list two rows at 2 / 0.
SELECT `command`, `group_id`, `subscription_id` FROM `permissions_commands`
WHERE `command` IN ('command_freeze', 'command_unfreeze');

-- pixelrp: :pull is staff or VIP (Twist, 2026-10-18), the same row shape as
-- :wd in 277 - group_id 2 lets in every staff rank (Trial Moderator and up),
-- and subscription_id 1 is the VIP way in for members of any rank
-- (PermissionManager reads a row as rank OR VIP). Players without VIP lose it.
--
-- Idempotent: an upsert.

INSERT INTO `permissions_commands` (`command`, `group_id`, `subscription_id`) VALUES
    ('command_pull', 2, 1)
ON DUPLICATE KEY UPDATE `group_id` = VALUES(`group_id`), `subscription_id` = VALUES(`subscription_id`);

-- Should list command_pull at 2 / 1.
SELECT `command`, `group_id`, `subscription_id` FROM `permissions_commands`
WHERE `command` = 'command_pull';

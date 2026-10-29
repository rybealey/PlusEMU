-- pixelrp: :armour / :armor hides the Kevlar vest that worn body armour puts
-- on the avatar, and shows it again (Twist, 2026-10-29). The armour is still
-- worn and still soaks; only the look changes.
--
-- 1. users.armour_hidden: 1 while the vest is hidden. On the user row so it
--    is loaded with the player and stays the way it was left after logging
--    out. Every existing account starts with the vest showing.
-- 2. command_armour: staff or VIP, the same row shape as :wd (277) and :pull
--    (278) - group_id 2 lets in every staff rank, subscription_id 1 is the
--    VIP way in. :armor uses the same permission.
--
-- Idempotent: the column is only added when missing, the row is an upsert.

SET @has_column = (
    SELECT COUNT(*) FROM information_schema.COLUMNS
    WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'users' AND COLUMN_NAME = 'armour_hidden'
);
SET @add_column = IF(@has_column = 0,
    'ALTER TABLE `users` ADD COLUMN `armour_hidden` TINYINT(1) NOT NULL DEFAULT 0 AFTER `rp_region`',
    'SELECT 1');
PREPARE add_column FROM @add_column;
EXECUTE add_column;
DEALLOCATE PREPARE add_column;

INSERT INTO `permissions_commands` (`command`, `group_id`, `subscription_id`) VALUES
    ('command_armour', 2, 1)
ON DUPLICATE KEY UPDATE `group_id` = VALUES(`group_id`), `subscription_id` = VALUES(`subscription_id`);

-- Should list command_armour at 2 / 1.
SELECT `command`, `group_id`, `subscription_id` FROM `permissions_commands`
WHERE `command` = 'command_armour';

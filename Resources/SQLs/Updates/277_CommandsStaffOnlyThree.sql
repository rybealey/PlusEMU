-- pixelrp: :ss and :setspeed are staff only, :wd is staff or VIP, and :hal
-- is gone (Twist, 2026-10-18).
--
-- Staff is every staff rank, Trial Moderator (2) and up - the same as the
-- rest of the clean-up (275, 276). :ss sets the state of the furni you are
-- facing; :setspeed sets a room's roller speed; :wd opens the clothes editor
-- (was Admin+).
--
-- :wd's subscription_id 1 is the VIP way in. PermissionManager reads a row as
-- rank OR VIP since this commit - group_id is the lowest rank, and a
-- subscription_id above 0 also lets in VIP members of any rank. No other row
-- has a subscription_id above 0 (275 and 276 moved the last ones to 0).
--
-- :hal is deleted in the same commit - :ha draws links in its message, so one
-- command does both. :ha's rank (5) is already below :hal's (6), so nobody who
-- could send a link alert loses the ability.
--
-- Idempotent: an upsert, and a delete that finds nothing the second time.

INSERT INTO `permissions_commands` (`command`, `group_id`, `subscription_id`) VALUES
    ('command_ss', 2, 0),
    ('command_setspeed', 2, 0),
    ('command_wd', 2, 1)
ON DUPLICATE KEY UPDATE `group_id` = VALUES(`group_id`), `subscription_id` = VALUES(`subscription_id`);

DELETE FROM `permissions_commands` WHERE `command` = 'command_hal';

-- Should list command_setspeed and command_ss at 2 / 0, and command_wd at 2 / 1,
-- and no command_hal.
SELECT `command`, `group_id`, `subscription_id` FROM `permissions_commands`
WHERE `command` IN ('command_ss', 'command_setspeed', 'command_wd', 'command_hal');

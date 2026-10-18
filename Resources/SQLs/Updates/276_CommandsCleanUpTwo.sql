-- pixelrp: the chat commands clean-up, second pass (Twist, 2026-10-18).
--
-- :dnd, :disablemimic and :disablediagonal are deleted in the same commit, and
-- :regenmaps and :bubble become staff only. Idempotent: the deletes find
-- nothing the second time, the staff rows are an upsert, and the reset only
-- touches rows that are still off.

-- 1. The deleted commands' permissions.
DELETE FROM `permissions_commands` WHERE `command` IN (
    'command_dnd',
    'command_disable_mimic',
    'command_disable_diagonal'
);

-- 2. :regenmaps and :bubble (was VIP) - every staff rank, Trial Moderator (2)
--    and up, with no VIP route. Choosing a chat bubble in Settings is not
--    this, and is unchanged.
INSERT INTO `permissions_commands` (`command`, `group_id`, `subscription_id`) VALUES
    ('command_regen_maps', 2, 0),
    ('command_bubble', 2, 0)
ON DUPLICATE KEY UPDATE `group_id` = 2, `subscription_id` = 0;

-- 3. :disablemimic saved its answer here, and with the command gone nobody
--    could switch it back - a player who had turned it off would be refused
--    by staff :mimic for good. Everybody is mimicable again.
UPDATE `users` SET `allow_mimic` = 1 WHERE `allow_mimic` = 0;

-- Should list only command_bubble and command_regen_maps, each at 2 / 0.
SELECT `command`, `group_id`, `subscription_id` FROM `permissions_commands`
WHERE `command` IN ('command_dnd', 'command_disable_mimic', 'command_disable_diagonal', 'command_regen_maps', 'command_bubble');

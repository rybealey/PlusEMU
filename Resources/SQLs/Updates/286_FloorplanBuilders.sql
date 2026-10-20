-- pixelrp: every staff rank, Builders included, can edit any room's floor
-- plan (Twist, 2026-10-21). :floorplan (and :floor, the same command under a
-- shorter word) was Administrator only (57); it comes down to Builder.
-- Saving the plan is Room.CanEditFloorplan: the room owner, or rank 2 and up
-- in any room.
--
-- group_id is a MINIMUM rank: 2 = Builder and every rank above it. Takes
-- effect when the emulator restarts, which the deploy does next.
--
-- Idempotent: an upsert.

INSERT INTO `permissions_commands` (`command`, `group_id`, `subscription_id`)
VALUES ('command_floorplan', 2, 0)
ON DUPLICATE KEY UPDATE `group_id` = VALUES(`group_id`), `subscription_id` = VALUES(`subscription_id`);

-- Should list one row at 2 / 0.
SELECT `command`, `group_id`, `subscription_id` FROM `permissions_commands`
WHERE `command` = 'command_floorplan';

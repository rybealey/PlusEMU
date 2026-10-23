-- pixelrp: a room's floor opacity (2026-10-23) - how see-through its floor
-- tiles are, 0 (gone) to 100 (solid). Staff set it with :topacity or the
-- Room tool's slider (RoomFloorOpacity); the client fades the floor planes.
--
--   rooms.floor_opacity - a percentage; every room starts solid.
--
-- :topacity goes to rank 5 and up - group_id is a MINIMUM rank - the same
-- staff as :roomsettings and the Room tool's settings (Room.CanManageSettings).
-- Takes effect when the emulator restarts, which the deploy does next.
--
-- The ALTER is guarded: the deploy runs under `set -e`. Idempotent.

SET @col := (SELECT COUNT(*) FROM information_schema.COLUMNS
  WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'rooms' AND COLUMN_NAME = 'floor_opacity');
SET @sql := IF(@col = 0,
  'ALTER TABLE `rooms` ADD COLUMN `floor_opacity` TINYINT UNSIGNED NOT NULL DEFAULT 100',
  'SELECT 1');
PREPARE s FROM @sql; EXECUTE s; DEALLOCATE PREPARE s;

INSERT INTO `permissions_commands` (`command`, `group_id`, `subscription_id`)
VALUES ('command_topacity', 5, 0)
ON DUPLICATE KEY UPDATE `group_id` = VALUES(`group_id`), `subscription_id` = VALUES(`subscription_id`);

-- Should list one row at 5 / 0.
SELECT `command`, `group_id`, `subscription_id` FROM `permissions_commands`
WHERE `command` = 'command_topacity';

-- pixelrp: Turf, a third zone type beside Safe and Unsafe.
--
-- A turf plays exactly as an unsafe zone - it is always stored with
-- is_safe_zone = '0', so every unsafe rule (combat, damage, the passive
-- countdown) applies without a line of new code - but a gang can CLAIM it
-- with :claim, by holding the room (TurfManager). The two-colour group furni
-- placed in a turf is drawn in the owning gang's colours, and in a neutral
-- grey pair while nobody holds it. That is display only: the furni's own
-- group (items_groups) is never rewritten.
--
-- Set from Room settings > Roleplay > Zoning, owner only, like Safe/Unsafe.
--
-- Idempotent.

-- MySQL has no ADD COLUMN IF NOT EXISTS, so the column goes in through a
-- prepared statement that is a no-op once it exists.
SET @has_turf := (SELECT COUNT(*) FROM `information_schema`.`COLUMNS`
                  WHERE `TABLE_SCHEMA` = DATABASE() AND `TABLE_NAME` = 'rooms' AND `COLUMN_NAME` = 'is_turf');
SET @ddl := IF(@has_turf = 0,
    'ALTER TABLE `rooms` ADD COLUMN `is_turf` enum(''0'',''1'') NOT NULL DEFAULT ''0'' AFTER `is_safe_zone`',
    'SELECT 1');
PREPARE turf_stmt FROM @ddl;
EXECUTE turf_stmt;
DEALLOCATE PREPARE turf_stmt;

-- Who holds each turf. One row per claimed room; an unclaimed turf has none.
CREATE TABLE IF NOT EXISTS `rp_turfs` (
  `room_id` int(11) NOT NULL,
  `gang_id` int(11) NOT NULL,
  `claimed_by` int(11) NOT NULL DEFAULT 0,
  `claimed_at` int(11) NOT NULL DEFAULT 0,
  PRIMARY KEY (`room_id`),
  KEY `gang_id` (`gang_id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

-- How long a gang has to hold a turf, uncontested, for :claim to complete.
INSERT INTO `server_settings` (`key`, `value`, `description`)
VALUES ('turf.capture.seconds', '60', 'pixelrp: seconds a gang must hold a turf room uncontested to claim it')
ON DUPLICATE KEY UPDATE `value` = `value`;

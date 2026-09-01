-- pixelrp: corporation headquarters, per-rank work authorization, and
-- room emergency-service access. Room-corp link + rank allow-list + a
-- per-corp service_type tag. All ALTERs guarded (prod rooms/rp_corporations
-- predate this and the deploy runs under `set -e`).

-- rooms.corporation_id (0 = not an HQ)
SET @col := (SELECT COUNT(*) FROM information_schema.COLUMNS
  WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'rooms' AND COLUMN_NAME = 'corporation_id');
SET @sql := IF(@col = 0,
  'ALTER TABLE `rooms` ADD COLUMN `corporation_id` INT NOT NULL DEFAULT 0',
  'SELECT 1');
PREPARE s FROM @sql; EXECUTE s; DEALLOCATE PREPARE s;

-- rooms.allow_medical / allow_police / allow_staff (emergency access, default on)
SET @col := (SELECT COUNT(*) FROM information_schema.COLUMNS
  WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'rooms' AND COLUMN_NAME = 'allow_medical');
SET @sql := IF(@col = 0,
  'ALTER TABLE `rooms` ADD COLUMN `allow_medical` ENUM(''0'',''1'') NOT NULL DEFAULT ''1''',
  'SELECT 1');
PREPARE s FROM @sql; EXECUTE s; DEALLOCATE PREPARE s;

SET @col := (SELECT COUNT(*) FROM information_schema.COLUMNS
  WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'rooms' AND COLUMN_NAME = 'allow_police');
SET @sql := IF(@col = 0,
  'ALTER TABLE `rooms` ADD COLUMN `allow_police` ENUM(''0'',''1'') NOT NULL DEFAULT ''1''',
  'SELECT 1');
PREPARE s FROM @sql; EXECUTE s; DEALLOCATE PREPARE s;

SET @col := (SELECT COUNT(*) FROM information_schema.COLUMNS
  WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'rooms' AND COLUMN_NAME = 'allow_staff');
SET @sql := IF(@col = 0,
  'ALTER TABLE `rooms` ADD COLUMN `allow_staff` ENUM(''0'',''1'') NOT NULL DEFAULT ''1''',
  'SELECT 1');
PREPARE s FROM @sql; EXECUTE s; DEALLOCATE PREPARE s;

-- rp_corporations.service_type ('', 'medical', 'police', 'staff')
SET @col := (SELECT COUNT(*) FROM information_schema.COLUMNS
  WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'rp_corporations' AND COLUMN_NAME = 'service_type');
SET @sql := IF(@col = 0,
  'ALTER TABLE `rp_corporations` ADD COLUMN `service_type` VARCHAR(12) NOT NULL DEFAULT ''''',
  'SELECT 1');
PREPARE s FROM @sql; EXECUTE s; DEALLOCATE PREPARE s;

-- Seed service tags by acronym (idempotent).
UPDATE `rp_corporations` SET `service_type` = 'medical' WHERE `acronym` = 'HMMC';
UPDATE `rp_corporations` SET `service_type` = 'police'  WHERE `acronym` = 'SFPD';
UPDATE `rp_corporations` SET `service_type` = 'staff'   WHERE `acronym` = 'PRPL';

-- Per-room authorized ranks (a row = that rank may work in that room).
CREATE TABLE IF NOT EXISTS `rp_hq_room_ranks` (
  `room_id` INT NOT NULL,
  `rank_id` INT NOT NULL,
  PRIMARY KEY (`room_id`, `rank_id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

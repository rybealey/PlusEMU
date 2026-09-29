-- pixelrp jail: :arrest, and the time served for it.
--
-- Two room tags, set by staff in the Room tool's Gameplay tab (RpSetEmergency
-- categories 3 and 4), and the sentences themselves.
--
--   rooms.rp_arrest_room - an officer can :arrest here, with the suspect on
--                          the room's Action Point.
--   rooms.rp_jail_room   - where an arrested player serves their time. Any
--                          room with the tag counts as the jail; a new
--                          prisoner is sent to the lowest-numbered one.
--
-- rp_jail is a record, like rp_charges: a sentence is never deleted, only
-- stamped `released_at` when it has been served. `seconds_left` is the clock -
-- it only runs while the prisoner is in the hotel, so it is saved as a number
-- of seconds rather than a release time, written on logout and once a minute
-- while they are online (JailState). A crash loses at most that minute.
--
-- All ALTERs guarded: the deploy runs under `set -e`. Idempotent.

SET @col := (SELECT COUNT(*) FROM information_schema.COLUMNS
  WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'rooms' AND COLUMN_NAME = 'rp_arrest_room');
SET @sql := IF(@col = 0,
  'ALTER TABLE `rooms` ADD COLUMN `rp_arrest_room` ENUM(''0'',''1'') NOT NULL DEFAULT ''0''',
  'SELECT 1');
PREPARE s FROM @sql; EXECUTE s; DEALLOCATE PREPARE s;

SET @col := (SELECT COUNT(*) FROM information_schema.COLUMNS
  WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'rooms' AND COLUMN_NAME = 'rp_jail_room');
SET @sql := IF(@col = 0,
  'ALTER TABLE `rooms` ADD COLUMN `rp_jail_room` ENUM(''0'',''1'') NOT NULL DEFAULT ''0''',
  'SELECT 1');
PREPARE s FROM @sql; EXECUTE s; DEALLOCATE PREPARE s;

CREATE TABLE IF NOT EXISTS `rp_jail` (
  `id` INT NOT NULL AUTO_INCREMENT,
  `user_id` INT NOT NULL,
  `officer_id` INT NOT NULL,
  `room_id` INT NOT NULL,
  `sentence_seconds` INT NOT NULL,
  `seconds_left` INT NOT NULL,
  `jailed_at` INT NOT NULL,
  `released_at` INT NOT NULL DEFAULT 0,
  PRIMARY KEY (`id`),
  KEY `idx_jail_user` (`user_id`, `released_at`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

-- pixelrp: a corporation can be hidden from the Corporations window
-- (RpGetCorpsEvent) - for everybody, staff included - from the City Panel's
-- Economy tab (Settings > Hide corporation). Hidden is only out of the
-- directory: its employees, shifts, pay and rooms carry on as before.
--
-- TINYINT(1), read through Dapper into a bool (CityEconomy.Corp.Hidden) and
-- compared in SQL - never stringified (see the tinyint-as-bool note).
--
-- Guarded: the deploy runs under `set -e`. Idempotent.

SET @col := (SELECT COUNT(*) FROM information_schema.COLUMNS
  WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'rp_corporations' AND COLUMN_NAME = 'hidden');
SET @sql := IF(@col = 0,
  'ALTER TABLE `rp_corporations` ADD COLUMN `hidden` TINYINT(1) NOT NULL DEFAULT 0',
  'SELECT 1');
PREPARE s FROM @sql; EXECUTE s; DEALLOCATE PREPARE s;

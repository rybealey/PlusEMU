-- PixelRP: :backup - an on-duty police officer asks the others for backup.
--
-- A request is a row in the 911 queue (rp_911_calls, 241), so it sits in 911
-- Dispatch with the calls, newest first, and is kept as history like them.
-- `kind` tells the two apart: 0 a 911 call, 1 a backup request. A request
-- carries no message and is never marked helpful or abuse.
--
-- A 911 call has one responder (the columns on rp_911_calls); a backup
-- request has as many as answer it, so they live in their own table. A
-- request never closes: any on-duty officer may still answer an old one.
--
-- Guarded: the deploy runs under `set -e`. Idempotent.

SET @col := (SELECT COUNT(*) FROM information_schema.COLUMNS
  WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'rp_911_calls' AND COLUMN_NAME = 'kind');
SET @sql := IF(@col = 0,
  'ALTER TABLE `rp_911_calls` ADD COLUMN `kind` TINYINT NOT NULL DEFAULT 0',
  'SELECT 1');
PREPARE s FROM @sql; EXECUTE s; DEALLOCATE PREPARE s;

CREATE TABLE IF NOT EXISTS `rp_911_responders` (
  `call_id` int NOT NULL,
  `officer_id` int NOT NULL,
  -- denormalised, as the calls are, so the list still reads after a rename
  `officer_name` varchar(32) NOT NULL DEFAULT '',
  `responded_at` int NOT NULL,
  PRIMARY KEY (`call_id`, `officer_id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

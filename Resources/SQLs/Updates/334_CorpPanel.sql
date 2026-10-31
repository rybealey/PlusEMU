-- pixelrp: the Corporation Panel (:cp), first slice - who may open it, the
-- weekly shift quota, shifts per day, and the weekly reset's record.
-- ALTERs guarded: the deploy runs under `set -e`.

-- rp_corporation_ranks.panel_access: this rank may open :cp for its own
-- corporation. can_approve: this rank may approve promotion and demotion
-- requests. Both TINYINT(1) (read back as bool).
SET @col := (SELECT COUNT(*) FROM information_schema.COLUMNS
  WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'rp_corporation_ranks' AND COLUMN_NAME = 'panel_access');
SET @sql := IF(@col = 0,
  'ALTER TABLE `rp_corporation_ranks` ADD COLUMN `panel_access` TINYINT(1) NOT NULL DEFAULT 0',
  'SELECT 1');
PREPARE s FROM @sql; EXECUTE s; DEALLOCATE PREPARE s;

SET @col := (SELECT COUNT(*) FROM information_schema.COLUMNS
  WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'rp_corporation_ranks' AND COLUMN_NAME = 'can_approve');
SET @sql := IF(@col = 0,
  'ALTER TABLE `rp_corporation_ranks` ADD COLUMN `can_approve` TINYINT(1) NOT NULL DEFAULT 0',
  'SELECT 1');
PREPARE s FROM @sql; EXECUTE s; DEALLOCATE PREPARE s;

-- Seed: the three leadership ranks of every corporation (the untiered ones,
-- migration 43 - Supervisor / Assistant Manager / General Manager style) open
-- the panel, and the top two approve. Staff tune it per corporation after.
UPDATE `rp_corporation_ranks` r
JOIN (
  SELECT `corporation_id`, MAX(`rank_order`) AS max_order
  FROM `rp_corporation_ranks` GROUP BY `corporation_id`
) m ON m.`corporation_id` = r.`corporation_id`
SET r.`panel_access` = (r.`rank_order` > m.max_order - 3),
    r.`can_approve` = (r.`rank_order` > m.max_order - 2);

-- rp_corporations.shift_quota: 10-minute shifts each employee should work a
-- week. 0 = no quota.
SET @col := (SELECT COUNT(*) FROM information_schema.COLUMNS
  WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'rp_corporations' AND COLUMN_NAME = 'shift_quota');
SET @sql := IF(@col = 0,
  'ALTER TABLE `rp_corporations` ADD COLUMN `shift_quota` INT NOT NULL DEFAULT 10',
  'SELECT 1');
PREPARE s FROM @sql; EXECUTE s; DEALLOCATE PREPARE s;

-- Seconds worked per character per UK calendar day, for the Weekly shifts
-- grid. Written by ShiftManager on every minute flush. Kept for the panel's
-- three weeks and the employee file's six; CorpWeek trims past 12 weeks.
CREATE TABLE IF NOT EXISTS `rp_corporation_shift_days` (
  `user_id` INT NOT NULL,
  `corporation_id` INT NOT NULL,
  `day` DATE NOT NULL,
  `seconds` INT NOT NULL DEFAULT 0,
  PRIMARY KEY (`user_id`, `day`, `corporation_id`),
  KEY `corp_day` (`corporation_id`, `day`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

-- One row per weekly reset that has run (Sunday 11:59 PM UK). week_start is
-- the Monday the new week is named for; CorpWeek runs a missed reset at boot.
CREATE TABLE IF NOT EXISTS `rp_corporation_weeks` (
  `week_start` DATE NOT NULL,
  `reset_at` INT NOT NULL,
  PRIMARY KEY (`week_start`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

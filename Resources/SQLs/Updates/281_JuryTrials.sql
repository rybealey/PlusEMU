-- pixelrp jury trials (Twist, 2026-10-19/20): three columns on each sentence.
--
--   rp_jail.trial_requested_at - when the prisoner asked for a trial; 0 = not
--                                asked. One request per sentence. A request a
--                                restart cut short (no trial_result yet) is
--                                given back when the emulator starts
--                                (JuryTrials.Init).
--   rp_jail.trial_result       - how it ended: 0 not yet, 1 guilty, 2 not
--                                guilty, 3 cancelled. A not-guilty prisoner who
--                                logged out between the verdict and the jail is
--                                let go when they arrive there.
--   rp_jail.charges            - the charge sheet the sentence was for, kept
--                                because :arrest drops the charges: one
--                                "count<TAB>crime name" line per crime, for
--                                the judge to read out.
--
-- All guarded: the deploy runs under `set -e`. Idempotent.

SET @col := (SELECT COUNT(*) FROM information_schema.COLUMNS
  WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'rp_jail' AND COLUMN_NAME = 'trial_requested_at');
SET @sql := IF(@col = 0,
  'ALTER TABLE `rp_jail` ADD COLUMN `trial_requested_at` INT NOT NULL DEFAULT 0',
  'SELECT 1');
PREPARE s FROM @sql; EXECUTE s; DEALLOCATE PREPARE s;

SET @col := (SELECT COUNT(*) FROM information_schema.COLUMNS
  WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'rp_jail' AND COLUMN_NAME = 'trial_result');
SET @sql := IF(@col = 0,
  'ALTER TABLE `rp_jail` ADD COLUMN `trial_result` TINYINT NOT NULL DEFAULT 0',
  'SELECT 1');
PREPARE s FROM @sql; EXECUTE s; DEALLOCATE PREPARE s;

SET @col := (SELECT COUNT(*) FROM information_schema.COLUMNS
  WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'rp_jail' AND COLUMN_NAME = 'charges');
SET @sql := IF(@col = 0,
  'ALTER TABLE `rp_jail` ADD COLUMN `charges` VARCHAR(1000) NOT NULL DEFAULT ''''',
  'SELECT 1');
PREPARE s FROM @sql; EXECUTE s; DEALLOCATE PREPARE s;

-- pixelrp: per-rank eligibility for emergency-service CONTINUE access.
-- Emergency services (Medical/Police) can no longer START a shift in a room
-- that admits their service - they clock in at their own HQ, then may keep
-- working in an admitting room. This flag says which ranks get that
-- continue access: Police = any rank; Medical = Paramedic and above.
-- (PixelRP Leadership / service_type 'staff' is handled unconditionally in
-- code and needs no flag.) TINYINT (read as int by Dapper, not the
-- enum/ToBool path). Guarded + idempotent; no-op where a corp isn't seeded.

SET @col := (SELECT COUNT(*) FROM information_schema.COLUMNS
  WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'rp_corporation_ranks' AND COLUMN_NAME = 'emergency_eligible');
SET @sql := IF(@col = 0,
  'ALTER TABLE `rp_corporation_ranks` ADD COLUMN `emergency_eligible` TINYINT NOT NULL DEFAULT 0',
  'SELECT 1');
PREPARE s FROM @sql; EXECUTE s; DEALLOCATE PREPARE s;

-- Police: every SFPD rank is eligible.
UPDATE `rp_corporation_ranks` r
  INNER JOIN `rp_corporations` c ON c.`id` = r.`corporation_id`
  SET r.`emergency_eligible` = 1
  WHERE c.`acronym` = 'SFPD';

-- Medical: HMMC ranks at Paramedic's rung and above.
SET @hmmc := (SELECT `id` FROM `rp_corporations` WHERE `acronym` = 'HMMC' LIMIT 1);
SET @para := (SELECT `rank_order` FROM `rp_corporation_ranks`
  WHERE `corporation_id` = @hmmc AND `name` = 'Paramedic' LIMIT 1);
UPDATE `rp_corporation_ranks`
  SET `emergency_eligible` = 1
  WHERE `corporation_id` = @hmmc AND @para IS NOT NULL AND `rank_order` >= @para;

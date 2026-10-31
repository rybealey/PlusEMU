-- pixelrp: Corporation Panel, slice 3 - promotions and demotions.
--
-- rp_corporation_rank_changes is the automatic log: one row per step up or
-- down a corporation's ladder (rank, then tier), however it happened -
-- :promote / :demote, or an approved request. Names are copied in so the log
-- still reads right after a rank is renamed or the employee leaves. The
-- employee's week at the time (shifts, warnings, strikes) is kept with it.
CREATE TABLE IF NOT EXISTS `rp_corporation_rank_changes` (
  `id` INT NOT NULL AUTO_INCREMENT,
  `corporation_id` INT NOT NULL,
  `user_id` INT NOT NULL,
  `username` VARCHAR(50) NOT NULL DEFAULT '',
  `direction` ENUM('promotion','demotion') NOT NULL,
  `from_rank_name` VARCHAR(50) NOT NULL DEFAULT '',
  `from_tier` INT NOT NULL DEFAULT 0,
  `to_rank_name` VARCHAR(50) NOT NULL DEFAULT '',
  `to_tier` INT NOT NULL DEFAULT 0,
  `reason` VARCHAR(255) NOT NULL DEFAULT '',
  `actor_id` INT NOT NULL DEFAULT 0,
  `actor_name` VARCHAR(50) NOT NULL DEFAULT '',
  `source` ENUM('command','request') NOT NULL DEFAULT 'command',
  `requested_by_name` VARCHAR(50) NOT NULL DEFAULT '',
  `shift_seconds_week` INT NOT NULL DEFAULT 0,
  `warnings` INT NOT NULL DEFAULT 0,
  `strikes` INT NOT NULL DEFAULT 0,
  `week_start` DATE NOT NULL,
  `created_at` INT NOT NULL,
  PRIMARY KEY (`id`),
  KEY `corp_week` (`corporation_id`, `week_start`),
  KEY `user_created` (`user_id`, `created_at`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

-- Requests: anyone the panel opens for asks, a rank with can_approve (not the
-- one who asked) decides. The step is worked out again when it is decided, so
-- a request made before some other change still moves exactly one step.
CREATE TABLE IF NOT EXISTS `rp_corporation_rank_requests` (
  `id` INT NOT NULL AUTO_INCREMENT,
  `corporation_id` INT NOT NULL,
  `user_id` INT NOT NULL,
  `direction` ENUM('promotion','demotion') NOT NULL,
  `reason` VARCHAR(255) NOT NULL DEFAULT '',
  `requested_by` INT NOT NULL DEFAULT 0,
  `requested_by_name` VARCHAR(50) NOT NULL DEFAULT '',
  `status` ENUM('pending','approved','denied','cancelled') NOT NULL DEFAULT 'pending',
  `decided_by_name` VARCHAR(50) NOT NULL DEFAULT '',
  `decided_at` INT NOT NULL DEFAULT 0,
  `note` VARCHAR(128) NOT NULL DEFAULT '',
  `created_at` INT NOT NULL,
  PRIMARY KEY (`id`),
  KEY `corp_status` (`corporation_id`, `status`),
  KEY `user_status` (`user_id`, `status`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

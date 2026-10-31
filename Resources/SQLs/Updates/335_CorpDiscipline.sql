-- pixelrp: Corporation Panel, slice 2 - warnings and strikes. One row per
-- warning or strike; week_start is the corporation week (CorpWeek) it was
-- issued in, so "this week" is a filter and nothing is wiped at the reset.
-- A revoked row stays, with who revoked it, so the employee file keeps the
-- whole story. automatic = the strike three warnings in one week added.
CREATE TABLE IF NOT EXISTS `rp_corporation_discipline` (
  `id` INT NOT NULL AUTO_INCREMENT,
  `corporation_id` INT NOT NULL,
  `user_id` INT NOT NULL,
  `kind` ENUM('warning','strike') NOT NULL,
  `reason` VARCHAR(64) NOT NULL,
  `details` VARCHAR(255) NOT NULL DEFAULT '',
  `issued_by` INT NOT NULL DEFAULT 0,
  `issued_by_name` VARCHAR(50) NOT NULL DEFAULT '',
  `automatic` TINYINT(1) NOT NULL DEFAULT 0,
  `week_start` DATE NOT NULL,
  `created_at` INT NOT NULL,
  `revoked_by_name` VARCHAR(50) NOT NULL DEFAULT '',
  `revoked_at` INT NOT NULL DEFAULT 0,
  PRIMARY KEY (`id`),
  KEY `corp_week` (`corporation_id`, `week_start`),
  KEY `user_created` (`user_id`, `created_at`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

-- pixelrp: Corporation Panel, slice 4 - leave of absence, the dismissals log
-- and the blacklist.

-- LOA: whole UK days, starts_on to ends_on inclusive. An employee asks with
-- :loa (pending); management logs one straight away (approved). While an
-- approved LOA covers today they can't clock in, and a week it touches counts
-- as quota met. Ending one early moves ends_on and marks it ended.
CREATE TABLE IF NOT EXISTS `rp_corporation_loa` (
  `id` INT NOT NULL AUTO_INCREMENT,
  `corporation_id` INT NOT NULL,
  `user_id` INT NOT NULL,
  `starts_on` DATE NOT NULL,
  `ends_on` DATE NOT NULL,
  `reason` VARCHAR(128) NOT NULL DEFAULT '',
  `status` ENUM('pending','approved','denied','cancelled','ended') NOT NULL DEFAULT 'pending',
  `requested_by` INT NOT NULL DEFAULT 0,
  `requested_by_name` VARCHAR(50) NOT NULL DEFAULT '',
  `decided_by_name` VARCHAR(50) NOT NULL DEFAULT '',
  `decided_at` INT NOT NULL DEFAULT 0,
  `created_at` INT NOT NULL,
  PRIMARY KEY (`id`),
  KEY `corp_dates` (`corporation_id`, `ends_on`),
  KEY `user_status` (`user_id`, `status`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

-- Dismissals: written by :fire, :quitjob and :superfire just before the
-- employee row goes, so the rank and time served are still known. Kept for
-- good; whether they can come back is the blacklist's say.
CREATE TABLE IF NOT EXISTS `rp_corporation_dismissals` (
  `id` INT NOT NULL AUTO_INCREMENT,
  `corporation_id` INT NOT NULL,
  `user_id` INT NOT NULL,
  `username` VARCHAR(50) NOT NULL DEFAULT '',
  `rank_name` VARCHAR(50) NOT NULL DEFAULT '',
  `tier` INT NOT NULL DEFAULT 0,
  `kind` ENUM('fired','quit','staff') NOT NULL,
  `reason` VARCHAR(255) NOT NULL DEFAULT '',
  `actor_id` INT NOT NULL DEFAULT 0,
  `actor_name` VARCHAR(50) NOT NULL DEFAULT '',
  `hired_at` INT NOT NULL DEFAULT 0,
  `created_at` INT NOT NULL,
  PRIMARY KEY (`id`),
  KEY `corp_created` (`corporation_id`, `created_at`),
  KEY `user_created` (`user_id`, `created_at`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

-- Blacklist: keyed by the ACCOUNT (users.parent_id, or the user itself), so a
-- player's other characters are refused too; user_id/username are the
-- character it was added for. expires_at 0 = permanent. Removing one keeps
-- the row, marked.
CREATE TABLE IF NOT EXISTS `rp_corporation_blacklist` (
  `id` INT NOT NULL AUTO_INCREMENT,
  `corporation_id` INT NOT NULL,
  `account_id` INT NOT NULL,
  `user_id` INT NOT NULL,
  `username` VARCHAR(50) NOT NULL DEFAULT '',
  `reason` VARCHAR(128) NOT NULL DEFAULT '',
  `added_by_name` VARCHAR(50) NOT NULL DEFAULT '',
  `created_at` INT NOT NULL,
  `expires_at` INT NOT NULL DEFAULT 0,
  `removed_by_name` VARCHAR(50) NOT NULL DEFAULT '',
  `removed_at` INT NOT NULL DEFAULT 0,
  PRIMARY KEY (`id`),
  KEY `corp_account` (`corporation_id`, `account_id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

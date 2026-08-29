-- Corporations: the foundation of the in-game economy. Players hold one job
-- (rank + tier I-V) in a corporation and earn coins per 10 minutes of shift
-- worked (payout system lands later; the schema carries what it needs).

CREATE TABLE IF NOT EXISTS `rp_corporations` (
  `id` int NOT NULL AUTO_INCREMENT,
  `name` varchar(64) NOT NULL,
  `description` varchar(255) NOT NULL DEFAULT '',
  -- badge code in c_images/album1584; '' falls back to the default corp
  -- badge (NPH17) client-side
  `badge` varchar(32) NOT NULL DEFAULT '',
  `sort_order` int NOT NULL DEFAULT 0,
  PRIMARY KEY (`id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

CREATE TABLE IF NOT EXISTS `rp_corporation_ranks` (
  `id` int NOT NULL AUTO_INCREMENT,
  -- 1 = lowest rank; display highest-first
  `corporation_id` int NOT NULL,
  `rank_order` int NOT NULL,
  `name` varchar(48) NOT NULL,
  -- coins per pay interval (10 minutes of shift worked)
  `pay` int NOT NULL DEFAULT 0,
  -- tier ceiling for employees at this rank (I..V)
  `tiers` int NOT NULL DEFAULT 5,
  PRIMARY KEY (`id`),
  KEY `idx_corp_rank` (`corporation_id`, `rank_order`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

CREATE TABLE IF NOT EXISTS `rp_corporation_employees` (
  -- one job per player
  `user_id` int NOT NULL,
  `corporation_id` int NOT NULL,
  `rank_id` int NOT NULL,
  `tier` int NOT NULL DEFAULT 1,
  `hired_at` int NOT NULL DEFAULT 0,
  -- shift bookkeeping for the payout system (lifetime / this week seconds)
  `shift_seconds` int NOT NULL DEFAULT 0,
  `shift_seconds_week` int NOT NULL DEFAULT 0,
  `on_duty` tinyint NOT NULL DEFAULT 0,
  PRIMARY KEY (`user_id`),
  KEY `idx_corp_employees` (`corporation_id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

-- First corporation: the San Francisco Police Department.
INSERT INTO `rp_corporations` (`id`, `name`, `description`, `badge`, `sort_order`)
VALUES (1, 'San Francisco Police Department', 'To protect and serve the city of San Francisco.', '', 1)
ON DUPLICATE KEY UPDATE `name` = VALUES(`name`);

INSERT INTO `rp_corporation_ranks` (`corporation_id`, `rank_order`, `name`, `pay`) VALUES
  (1, 1, 'Cadet', 15),
  (1, 2, 'Officer', 17),
  (1, 3, 'Sergeant', 19),
  (1, 4, 'Lieutenant', 21),
  (1, 5, 'Captain', 23),
  (1, 6, 'Deputy Chief', 25),
  (1, 7, 'Police Chief', 27);

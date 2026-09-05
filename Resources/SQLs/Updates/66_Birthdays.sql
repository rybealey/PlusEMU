-- pixelrp: player birthday (phone Settings > Account). Day and month only -
-- no year is ever stored.
CREATE TABLE IF NOT EXISTS `rp_user_birthdays` (
  `user_id` int(11) NOT NULL,
  `month` tinyint(4) NOT NULL,
  `day` tinyint(4) NOT NULL,
  PRIMARY KEY (`user_id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

-- PixelRP: backpack trading (RpTrades) - every completed trade, for the City
-- Panel's Trade History.
--
-- A record: never edited, never deleted. Names and the room are kept as they
-- were, so a trade still reads after a rename. What each side gave is
-- "item:count,item:count" - the backpack's own item keys.
CREATE TABLE IF NOT EXISTS `rp_trades` (
  `id` int NOT NULL AUTO_INCREMENT,
  `user_a` int NOT NULL,
  `name_a` varchar(32) NOT NULL DEFAULT '',
  `user_b` int NOT NULL,
  `name_b` varchar(32) NOT NULL DEFAULT '',
  `room_id` int NOT NULL DEFAULT 0,
  `room_name` varchar(64) NOT NULL DEFAULT '',
  `gave_a` varchar(512) NOT NULL DEFAULT '',
  `gave_b` varchar(512) NOT NULL DEFAULT '',
  `created_at` int NOT NULL,
  PRIMARY KEY (`id`),
  KEY `user_a` (`user_a`, `id`),
  KEY `user_b` (`user_b`, `id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

-- pixelrp: every connection disconnected for flooding the server with packets
-- (PacketBudget, PacketFloodLog) - far more of one kind than the client ever
-- sends, which is a packet tool or a bot. Shown on the housekeeping player
-- page, for every character on the account.
--
-- packet: the packet dropped most in the window, e.g. MoveAvatarEvent.
-- dropped: every packet over the limit in that window, of any kind.
--
-- Idempotent: CREATE TABLE IF NOT EXISTS.

CREATE TABLE IF NOT EXISTS `rp_packet_flood_log` (
  `id` int NOT NULL AUTO_INCREMENT,
  `user_id` int NOT NULL,
  `username` varchar(64) NOT NULL DEFAULT '',
  `packet` varchar(64) NOT NULL DEFAULT '',
  `dropped` int NOT NULL DEFAULT 0,
  `window_seconds` int NOT NULL DEFAULT 0,
  `created_at` int NOT NULL,
  PRIMARY KEY (`id`),
  KEY `user_created` (`user_id`, `created_at`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

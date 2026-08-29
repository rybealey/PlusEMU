-- Discord account linking (Settings > Social > Verification > Discord).
-- discord_id binds one Discord account per player; the sync queue carries
-- emulator events (login/logout/vip) to the CMS scheduler, which owns every
-- Discord API call.

ALTER TABLE `users`
  ADD COLUMN `discord_id` varchar(32) NULL DEFAULT NULL,
  ADD COLUMN `discord_linked_at` int NOT NULL DEFAULT 0,
  ADD UNIQUE KEY `idx_users_discord_id` (`discord_id`);

CREATE TABLE IF NOT EXISTS `discord_sync_queue` (
  `id` int NOT NULL AUTO_INCREMENT,
  `user_id` int NOT NULL,
  `reason` varchar(24) NOT NULL DEFAULT '',
  `created_at` int NOT NULL DEFAULT 0,
  PRIMARY KEY (`id`),
  KEY `idx_dsq_user` (`user_id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

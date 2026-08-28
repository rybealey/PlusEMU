-- PixelRP photo albums for the phone's Photos app (Collections tab).
-- Personal albums hold the owner's photos; shared albums additionally carry
-- members (friends the owner invited) who can view and contribute their own
-- photos. The Screenshots album is virtual (source-based) and has no rows
-- here. People/Places groupings come from photo metadata, not these tables.
CREATE TABLE IF NOT EXISTS `camera_web_albums` (
  `id` BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
  `owner_id` INT NOT NULL,
  `name` VARCHAR(50) NOT NULL,
  `is_shared` TINYINT(1) NOT NULL DEFAULT 0,
  `created_at` INT UNSIGNED NOT NULL DEFAULT 0,
  PRIMARY KEY (`id`),
  KEY `idx_albums_owner` (`owner_id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

CREATE TABLE IF NOT EXISTS `camera_web_album_members` (
  `id` BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
  `album_id` BIGINT UNSIGNED NOT NULL,
  `user_id` INT NOT NULL,
  PRIMARY KEY (`id`),
  UNIQUE KEY `uq_album_member` (`album_id`, `user_id`),
  KEY `idx_album_members_user` (`user_id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

CREATE TABLE IF NOT EXISTS `camera_web_album_photos` (
  `id` BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
  `album_id` BIGINT UNSIGNED NOT NULL,
  `photo_id` BIGINT UNSIGNED NOT NULL,
  PRIMARY KEY (`id`),
  UNIQUE KEY `uq_album_photo` (`album_id`, `photo_id`),
  KEY `idx_album_photos_album` (`album_id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

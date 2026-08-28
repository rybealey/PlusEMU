-- PixelRP photo metadata: each camera_web row now records how it was captured
-- ('camera' = phone/room camera shot, 'screenshot' = phone side-button,
-- 'saved' = a photo received in a DM and saved to the library; '' = legacy
-- rows from before this migration) plus a snapshot of the room's name at
-- capture time (survives room renames/deletions). Phone camera shots also
-- record which players were inside the frame, one row per tagged player in
-- camera_web_users (validated server-side against the room's roster).
-- Not shown anywhere yet - groundwork for expanding the Photos app.
ALTER TABLE `camera_web`
  ADD COLUMN `source` VARCHAR(16) NOT NULL DEFAULT '',
  ADD COLUMN `room_name` VARCHAR(100) NOT NULL DEFAULT '';

CREATE TABLE IF NOT EXISTS `camera_web_users` (
  `id` BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
  `photo_id` BIGINT UNSIGNED NOT NULL,
  `user_id` INT NOT NULL,
  `username` VARCHAR(100) NOT NULL DEFAULT '',
  PRIMARY KEY (`id`),
  KEY `idx_camera_web_users_photo` (`photo_id`),
  KEY `idx_camera_web_users_user` (`user_id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

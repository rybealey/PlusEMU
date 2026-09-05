-- pixelrp: staff-scheduled in-game events for the phone's Calendar app.
-- Times are unix seconds; colour is '#rrggbb'; room_id 0 = no room.
CREATE TABLE IF NOT EXISTS `rp_events` (
  `id` int(11) NOT NULL AUTO_INCREMENT,
  `title` varchar(64) NOT NULL,
  `description` text NOT NULL,
  `starts_at` int(11) NOT NULL,
  `ends_at` int(11) NOT NULL,
  `room_id` int(11) NOT NULL DEFAULT 0,
  `colour` varchar(7) NOT NULL DEFAULT '#3f8fbf',
  `host_name` varchar(32) NOT NULL DEFAULT '',
  `created_by` int(11) NOT NULL,
  `created_at` int(11) NOT NULL,
  PRIMARY KEY (`id`),
  KEY `starts` (`starts_at`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

-- PixelRP: 911 calls - :911 <message> (or :999), answered from the
-- Emergency Calls window every on-duty officer has open (EmergencyCalls).
--
-- A record, like rp_charges: a call is never deleted. The window shows the
-- newest; the rest are history. Who called, from where and what they looked
-- like are denormalised so a call still reads after a rename, a room rename
-- or a change of clothes.
--
-- `mark` is set once and never changed: 1 helpful (the caller is paid), 2
-- abuse (the caller is charged with 911abuse by the responding officer).
CREATE TABLE IF NOT EXISTS `rp_911_calls` (
  `id` int NOT NULL AUTO_INCREMENT,
  `caller_id` int NOT NULL,
  `caller_name` varchar(32) NOT NULL DEFAULT '',
  `caller_look` varchar(512) NOT NULL DEFAULT '',
  `caller_gender` varchar(1) NOT NULL DEFAULT 'M',
  `room_id` int NOT NULL DEFAULT 0,
  `room_name` varchar(64) NOT NULL DEFAULT '',
  `message` varchar(200) NOT NULL DEFAULT '',
  `created_at` int NOT NULL,
  `responder_id` int NOT NULL DEFAULT 0,
  `responder_name` varchar(32) NOT NULL DEFAULT '',
  `responded_at` int NOT NULL DEFAULT 0,
  `mark` tinyint NOT NULL DEFAULT 0,
  `marked_by_id` int NOT NULL DEFAULT 0,
  `marked_by_name` varchar(32) NOT NULL DEFAULT '',
  `marked_at` int NOT NULL DEFAULT 0,
  PRIMARY KEY (`id`),
  KEY `caller` (`caller_id`, `id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

-- PixelRP: a record of bin searches (Twist, 2026-10-18).
--
-- rp_bin_search_log: one row per finished search (HabboHotel/Rooms/BinSearch),
-- whatever it found - 'nothing' included - with the character, their account
-- (`users`.`parent_id`, or their own id for a root), the bin, the room, the
-- find, how much of it, and how much was actually carried away (`given`; a
-- full backpack can leave some in the bin).
--
-- It is also the count behind the daily limit: an account finishes at most 5
-- searches in any 24 hours, every character on it together - hence the index
-- on account and time.
--
-- Idempotent.

CREATE TABLE IF NOT EXISTS `rp_bin_search_log` (
  `id` int NOT NULL AUTO_INCREMENT,
  `user_id` int NOT NULL,
  `account_id` int NOT NULL,
  `item_id` int unsigned NOT NULL,
  `room_id` int unsigned NOT NULL,
  `reward` varchar(32) NOT NULL,
  `amount` int NOT NULL DEFAULT 0,
  `given` int NOT NULL DEFAULT 0,
  `created_at` int NOT NULL,
  PRIMARY KEY (`id`),
  KEY `account_time` (`account_id`, `created_at`),
  KEY `user_time` (`user_id`, `created_at`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

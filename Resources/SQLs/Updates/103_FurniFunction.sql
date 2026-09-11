-- PixelRP: the Function window - editing furni behaviour from inside the room.
--
-- Builders need walkable / sittable / layable / stackable on the furni they
-- build with, and until now that meant a developer running UPDATE against
-- `furniture`. The Function button on the infostand opens an editor for the
-- DEFINITION, so a change lands on every placed copy hotel-wide and on
-- everything bought afterwards.
--
-- Two things this needs from the database:
--
-- 1. A permission. Rank 7 and up, as its own key rather than a bare rank
--    check, so it can be granted or withheld independently of the other
--    rank-7 rights.
-- 2. An audit trail. This edits a global definition - one careless change
--    reaches the whole hotel - so every applied change records who made it
--    and what it was, old value and new. Without it a bad edit is
--    untraceable: the furni simply behaves differently and nobody knows why.
--
-- The log is deliberately one row PER FIELD rather than per submit. A submit
-- that changes three fields is three rows, which makes "when did this furni
-- become walkable" answerable with a single WHERE.

CREATE TABLE IF NOT EXISTS `rp_furni_function_log` (
  `id` int NOT NULL AUTO_INCREMENT,
  `definition_id` int unsigned NOT NULL,
  -- denormalised so the log still reads after a furni is renamed or removed
  `item_name` varchar(70) NOT NULL DEFAULT '',
  `user_id` int NOT NULL,
  `username` varchar(32) NOT NULL DEFAULT '',
  `field` varchar(32) NOT NULL,
  `old_value` varchar(255) NOT NULL DEFAULT '',
  `new_value` varchar(255) NOT NULL DEFAULT '',
  `changed_at` timestamp NOT NULL DEFAULT CURRENT_TIMESTAMP,
  PRIMARY KEY (`id`),
  KEY `definition_id` (`definition_id`),
  KEY `user_id` (`user_id`),
  KEY `changed_at` (`changed_at`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

INSERT INTO `permissions_commands` (`command`, `group_id`, `subscription_id`)
VALUES ('rp_furni_function', 7, 0)
ON DUPLICATE KEY UPDATE `group_id` = 7;

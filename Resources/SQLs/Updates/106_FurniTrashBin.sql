-- pixelrp: the inventory's trash bin.
--
-- Players can destroy their own unplaced furni from the inventory window. The
-- item row is erased - there is no refund and nothing to restore - so the only
-- record that it ever existed is this log.
--
-- Every column except the timestamp is denormalised on purpose. The item is
-- gone by the time anyone reads this, so an id joined against `items` would
-- find nothing; the furni may also be renamed later, and a rap sheet that
-- changes wording under you is worse than one that is slightly stale. What is
-- written here is what was true at the moment the player pressed the button.
--
-- `item_id` is kept even though the row it named no longer exists: item ids
-- are handed out by AUTO_INCREMENT and never reused in practice, so it is what
-- ties a complaint ("I binned it by accident") to a specific act, and what
-- lines this log up against the trade and marketplace history for the same id.
CREATE TABLE IF NOT EXISTS `rp_furni_delete_log` (
  `id` int NOT NULL AUTO_INCREMENT,
  `item_id` int unsigned NOT NULL,
  `definition_id` int unsigned NOT NULL,
  `item_name` varchar(70) NOT NULL DEFAULT '',
  `user_id` int NOT NULL,
  `username` varchar(32) NOT NULL DEFAULT '',
  `deleted_at` timestamp NOT NULL DEFAULT CURRENT_TIMESTAMP,
  PRIMARY KEY (`id`),
  KEY `item_id` (`item_id`),
  KEY `user_id` (`user_id`),
  KEY `deleted_at` (`deleted_at`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

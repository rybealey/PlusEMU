-- PixelRP: staff move a furni to another category from the shop itself
-- (RpCatalogMoveFurniEvent), and every move is written down here.
--
-- Two reasons to keep it. Who moved what, and from where, when a furni goes
-- missing from the page somebody expected it on. And the shop's earlier
-- reorganising was all done in migrations so every database gets it, while a
-- move made in the shop changes only the database it was made in - this log
-- holds enough (the furni's classname, both pages' captions as well as their
-- ids) to write those moves up as a migration for another database later.

CREATE TABLE IF NOT EXISTS `rp_catalog_move_log` (
  `id` int NOT NULL AUTO_INCREMENT,
  `catalog_item_id` int NOT NULL,
  `definition_id` int unsigned NOT NULL,
  -- denormalised so the log still reads after a furni or page is renamed or removed
  `item_name` varchar(70) NOT NULL DEFAULT '',
  `from_page_id` int NOT NULL,
  `from_caption` varchar(128) NOT NULL DEFAULT '',
  `to_page_id` int NOT NULL,
  `to_caption` varchar(128) NOT NULL DEFAULT '',
  `user_id` int NOT NULL,
  `username` varchar(32) NOT NULL DEFAULT '',
  `moved_at` timestamp NOT NULL DEFAULT CURRENT_TIMESTAMP,
  PRIMARY KEY (`id`),
  KEY `catalog_item_id` (`catalog_item_id`),
  KEY `moved_at` (`moved_at`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

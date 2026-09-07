-- pixelrp: the Clothing Store (:zara). catalog_clothing already lists every
-- sellable set (name + figuredata part ids); the store adds what it needs to
-- sell them:
--   display_name  what the shelf shows (NULL = the client tidies clothing_name)
--   price         credits; 0 hides the piece from the store
--   ltd_total     > 0 makes the piece a limited edition sold as a backpack
--                 token, this many copies in total
--   ltd_sold      copies sold so far (edition numbers count up from 1)
ALTER TABLE `catalog_clothing`
  ADD COLUMN `display_name` varchar(64) NULL DEFAULT NULL AFTER `clothing_parts`,
  ADD COLUMN `price` int(11) NOT NULL DEFAULT 0 AFTER `display_name`,
  ADD COLUMN `ltd_total` int(11) NOT NULL DEFAULT 0 AFTER `price`,
  ADD COLUMN `ltd_sold` int(11) NOT NULL DEFAULT 0 AFTER `ltd_total`;

-- Opening stock: every existing set goes on the shelf at 50 credits so the
-- store is not empty on day one. Retune per piece (or set 0 to pull one).
UPDATE `catalog_clothing` SET `price` = 50 WHERE `price` = 0;

-- :zara opens the store for every player (group_id 1, like :passive).
INSERT INTO `permissions_commands` (`command`, `group_id`, `subscription_id`)
VALUES ('command_zara', 1, 0)
ON DUPLICATE KEY UPDATE `group_id` = 1;

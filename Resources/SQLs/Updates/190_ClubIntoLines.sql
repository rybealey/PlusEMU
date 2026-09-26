-- pixelrp: Builders > Club moves to Furni > Lines.
--
-- Club is one of the Builders tab's own pages - it exists on the server and
-- in no migration - so it is found by caption directly under Builders, and
-- moves whole, with whatever is under it.
--
-- Builders pages carry rank 2 on themselves as well as on the tab (84, 92,
-- 93 all wrote min_rank 2), and a page's own rank still gates it wherever it
-- is. So Club and everything under it drop from 2 to 1 as they move, to be
-- open to everyone like the rest of Lines. A rank set higher than 2 - a
-- staff-only shelf - is left alone.
--
-- Lines is alphabetical (175 and 177 kept it so), and is renumbered after.
-- Idempotent: once Club is under Lines it is no longer found under Builders.

SET @builders := (SELECT `id` FROM `catalog_pages` WHERE `parent_id` = -1 AND `caption` = 'Builders' LIMIT 1);
SET @furni := (SELECT `id` FROM `catalog_pages` WHERE `parent_id` = -1 AND `page_link` = 'furni' LIMIT 1);
SET @lines := (SELECT `id` FROM `catalog_pages` WHERE `parent_id` = @furni AND `caption` = 'Lines' LIMIT 1);
SET @club := (SELECT `id` FROM `catalog_pages` WHERE `parent_id` = @builders AND `caption` = 'Club' ORDER BY `id` LIMIT 1);

-- Club and everything under it, before it moves.
DROP TABLE IF EXISTS `_club_pages`;
CREATE TABLE `_club_pages` (`id` INT NOT NULL PRIMARY KEY) ENGINE=InnoDB;
INSERT IGNORE INTO `_club_pages` (`id`) SELECT `id` FROM `catalog_pages` WHERE `id` = @club AND @lines IS NOT NULL;
INSERT IGNORE INTO `_club_pages` (`id`)
    SELECT c.`id` FROM `catalog_pages` c JOIN `_club_pages` p ON c.`parent_id` = p.`id`;
INSERT IGNORE INTO `_club_pages` (`id`)
    SELECT c.`id` FROM `catalog_pages` c JOIN `_club_pages` p ON c.`parent_id` = p.`id`;
INSERT IGNORE INTO `_club_pages` (`id`)
    SELECT c.`id` FROM `catalog_pages` c JOIN `_club_pages` p ON c.`parent_id` = p.`id`;

UPDATE `catalog_pages` p JOIN `_club_pages` c ON c.`id` = p.`id`
   SET p.`min_rank` = 1
 WHERE p.`min_rank` = 2;

UPDATE `catalog_pages` SET `parent_id` = @lines WHERE `id` = @club AND @lines IS NOT NULL;

DROP TABLE `_club_pages`;

SET @n := 0;
UPDATE `catalog_pages` SET `order_num` = (@n := @n + 1)
 WHERE `parent_id` = @lines AND @lines IS NOT NULL
 ORDER BY `caption`;

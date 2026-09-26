-- pixelrp: Builders Club's own shape pages move into Builders Club > Shapes.
--
-- The renamed Blocks page (184) came with shape pages of its own - they exist
-- on the server and in no migration, so they are found live: every page under
-- Builders Club from 'Cone' to 'Triangular Prism' in the tab's order, both
-- ends included, and the pages 'Round' and 'Small'. They move under Shapes
-- (182's page), which is then put in alphabetical order as it was.
--
-- Shapes, Materials, Alphabet and Custom Alphabets never move, wherever they
-- fall. If 'Cone' or 'Triangular Prism' is not found, the range moves nothing
-- rather than guessing at one; Round and Small still move.
--
-- Idempotent: once moved, the pages are no longer under Builders Club.

SET @builders := (SELECT `id` FROM `catalog_pages` WHERE `parent_id` = -1 AND `caption` = 'Builders' LIMIT 1);
SET @bc := (SELECT `id` FROM `catalog_pages` WHERE `parent_id` = @builders AND `caption` = 'Builders Club' ORDER BY `id` LIMIT 1);
SET @shapes := (SELECT `id` FROM `catalog_pages` WHERE `parent_id` = @bc AND `caption` = 'Shapes' ORDER BY `id` LIMIT 1);
SET @from := (SELECT `order_num` FROM `catalog_pages` WHERE `parent_id` = @bc AND `caption` = 'Cone' ORDER BY `order_num` LIMIT 1);
SET @to := (SELECT `order_num` FROM `catalog_pages` WHERE `parent_id` = @bc AND `caption` = 'Triangular Prism' ORDER BY `order_num` DESC LIMIT 1);

DROP TABLE IF EXISTS `_bcs_move`;
CREATE TABLE `_bcs_move` (`id` INT NOT NULL PRIMARY KEY) ENGINE=InnoDB;
INSERT IGNORE INTO `_bcs_move` (`id`)
    SELECT `id` FROM `catalog_pages`
     WHERE `parent_id` = @bc AND @shapes IS NOT NULL
       AND `caption` NOT IN ('Shapes', 'Materials', 'Alphabet', 'Custom Alphabets', 'Letters & Numbers')
       AND ((@from IS NOT NULL AND @to IS NOT NULL AND `order_num` BETWEEN LEAST(@from, @to) AND GREATEST(@from, @to))
            OR `caption` IN ('Round', 'Small'));

UPDATE `catalog_pages` p JOIN `_bcs_move` m ON m.`id` = p.`id` SET p.`parent_id` = @shapes;

DROP TABLE `_bcs_move`;

SET @n := 0;
UPDATE `catalog_pages` SET `order_num` = (@n := @n + 1)
 WHERE `parent_id` = @shapes AND @shapes IS NOT NULL
 ORDER BY `caption`;

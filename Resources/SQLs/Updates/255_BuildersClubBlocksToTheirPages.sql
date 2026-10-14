-- pixelrp: the blocks sitting on Builders > Builders Club itself go to the
-- Builders Club sub-category of their own material.
--
-- 189 sent Construction's plain (untinted) material blocks, furniture
-- 104326-104342, to Builders Club > Materials > <material> - and, where that
-- page was not there, to Materials, failing that to Builders Club. On the
-- server the material pages are not under a Materials page at all: they hang
-- straight off Builders Club, from the tab's own Blocks page (184), with
-- captions of their own (Simple Wood, not Wood). So the blocks 189 moved all
-- landed on Builders Club itself, in a row above its categories.
--
-- Each block now goes where the rest of its material already is: the page
-- under Builders Club that sells the most of Habbo's official set it shares a
-- bundle with (bc_block_water*N for the Water block, and so on). That finds
-- the page whatever it is called. Only if no page sells the set is a caption
-- tried. A block that page already sells comes off Builders Club instead of
-- being listed twice. One that finds nowhere stays where it is.
--
-- Anything else on Builders Club itself whose furni family (its classname
-- before the '*') already has a page under Builders Club goes there the same
-- way. Nothing is created and no other page is touched; the furniture rows,
-- and every copy anybody owns, are untouched.
--
-- Idempotent: a second run finds nothing on Builders Club that has a page.

SET @builders := (SELECT `id` FROM `catalog_pages` WHERE `parent_id` = -1 AND `caption` = 'Builders' LIMIT 1);
SET @bc := (SELECT `id` FROM `catalog_pages` WHERE `parent_id` = @builders AND `caption` = 'Builders Club' ORDER BY `id` LIMIT 1);

-- Builders Club's sub-categories, three levels down: the pages a block may
-- go to. Furni grids only.
DROP TABLE IF EXISTS `_bcb_tree`;
CREATE TABLE `_bcb_tree` (`page_id` INT NOT NULL PRIMARY KEY) ENGINE=InnoDB;
INSERT IGNORE INTO `_bcb_tree` (`page_id`)
    SELECT `id` FROM `catalog_pages` WHERE `parent_id` = @bc AND @bc IS NOT NULL;
INSERT IGNORE INTO `_bcb_tree` (`page_id`)
    SELECT p.`id` FROM `catalog_pages` p JOIN (SELECT `page_id` FROM `_bcb_tree`) t ON p.`parent_id` = t.`page_id`;
INSERT IGNORE INTO `_bcb_tree` (`page_id`)
    SELECT p.`id` FROM `catalog_pages` p JOIN (SELECT `page_id` FROM `_bcb_tree`) t ON p.`parent_id` = t.`page_id`;
DELETE t FROM `_bcb_tree` t JOIN `catalog_pages` p ON p.`id` = t.`page_id`
 WHERE p.`enabled` <> b'1' OR p.`page_layout` NOT IN ('default_3x3', 'default_3x3_color_grouping');

-- How many of each furni family every one of those pages sells.
DROP TABLE IF EXISTS `_bcb_family`;
CREATE TABLE `_bcb_family` (`family` VARCHAR(120) NOT NULL, `page_id` INT NOT NULL, `listed` INT NOT NULL,
    PRIMARY KEY (`family`, `page_id`)) ENGINE=InnoDB DEFAULT CHARSET=latin1;
INSERT INTO `_bcb_family` (`family`, `page_id`, `listed`)
    SELECT SUBSTRING_INDEX(f.`item_name`, '*', 1), ci.`page_id`, COUNT(*)
      FROM `catalog_items` ci
      JOIN `_bcb_tree` t ON t.`page_id` = ci.`page_id`
      JOIN `furniture` f ON CAST(f.`id` AS CHAR) = ci.`item_id`
     GROUP BY SUBSTRING_INDEX(f.`item_name`, '*', 1), ci.`page_id`;

-- Every row on Builders Club itself, with the family it belongs with. The
-- Construction blocks carry classnames of their own, so theirs is named.
DROP TABLE IF EXISTS `_bcb_move`;
CREATE TABLE `_bcb_move` (`catalog_item_id` INT NOT NULL PRIMARY KEY, `item_id` VARCHAR(120) NOT NULL,
    `family` VARCHAR(120) NOT NULL, `caption` VARCHAR(35) NULL, `alt_caption` VARCHAR(35) NULL,
    `target` INT NULL) ENGINE=InnoDB DEFAULT CHARSET=latin1;

DROP TABLE IF EXISTS `_bcb_block`;
CREATE TABLE `_bcb_block` (`furni_id` INT NOT NULL PRIMARY KEY, `family` VARCHAR(40) NOT NULL,
    `caption` VARCHAR(35) NOT NULL, `alt_caption` VARCHAR(35) NULL) ENGINE=InnoDB DEFAULT CHARSET=latin1;
INSERT INTO `_bcb_block` (`furni_id`, `family`, `caption`, `alt_caption`) VALUES
    (104326, 'bc_block_water', 'Water', NULL),
    (104327, 'bc_block_sand', 'Sand', NULL),
    (104328, 'bc_block_artdeco1', 'Art Deco', NULL),
    (104329, 'bc_block_redbrick', 'Brick', NULL),
    (104330, 'bc_metalcrate', 'Metal Crate', NULL),
    (104331, 'bc_block_glass', 'Glass', 'Crystal'),
    (104332, 'bc_block_grass', 'Grass', NULL),
    (104333, 'bc_block_industrial', 'Industrial', NULL),
    (104334, 'bc_lavarock', 'Lava', NULL),
    (104335, 'bc_block_marble', 'Marble', NULL),
    (104336, 'bc_block_metalgrip', 'Metal', 'Metal Grip'),
    (104337, 'bc_wood', 'Wood', 'Simple Wood'),
    (104338, 'bc_tile', 'Tile', NULL),
    (104339, 'bc_flowerhedge', 'Flower Hedge', NULL),
    (104340, 'bc_stone', 'Stone', NULL),
    (104341, 'bc_terra', 'Terra', NULL),
    (104342, 'bc_block_wool', 'Wool', NULL);

INSERT INTO `_bcb_move` (`catalog_item_id`, `item_id`, `family`, `caption`, `alt_caption`)
    SELECT ci.`id`, ci.`item_id`, COALESCE(b.`family`, SUBSTRING_INDEX(f.`item_name`, '*', 1)), b.`caption`, b.`alt_caption`
      FROM `catalog_items` ci
      JOIN `furniture` f ON CAST(f.`id` AS CHAR) = ci.`item_id`
      LEFT JOIN `_bcb_block` b ON b.`furni_id` = f.`id`
     WHERE ci.`page_id` = @bc AND @bc IS NOT NULL;

-- Where the rest of the family is: the page selling the most of it.
UPDATE `_bcb_move` m
   SET m.`target` = (SELECT x.`page_id` FROM `_bcb_family` x WHERE x.`family` = m.`family`
                      ORDER BY x.`listed` DESC, x.`page_id` LIMIT 1);

-- No page sells the family: a Construction block tries its material's name.
UPDATE `_bcb_move` m
   SET m.`target` = (SELECT p.`id` FROM `catalog_pages` p JOIN `_bcb_tree` t ON t.`page_id` = p.`id`
                      WHERE p.`caption` IN (m.`caption`, m.`alt_caption`) ORDER BY p.`id` LIMIT 1)
 WHERE m.`target` IS NULL AND m.`caption` IS NOT NULL;

-- Already sold where it would go: off Builders Club rather than in twice.
DELETE ci FROM `catalog_items` ci
  JOIN `_bcb_move` m ON m.`catalog_item_id` = ci.`id`
 WHERE ci.`page_id` = @bc AND m.`target` IS NOT NULL
   AND EXISTS (SELECT 1 FROM (SELECT `page_id`, `item_id` FROM `catalog_items`) x
                WHERE x.`page_id` = m.`target` AND x.`item_id` = ci.`item_id`);

UPDATE `catalog_items` ci
  JOIN `_bcb_move` m ON m.`catalog_item_id` = ci.`id`
   SET ci.`page_id` = m.`target`
 WHERE ci.`page_id` = @bc AND m.`target` IS NOT NULL;

DROP TABLE `_bcb_block`;
DROP TABLE `_bcb_move`;
DROP TABLE `_bcb_family`;
DROP TABLE `_bcb_tree`;

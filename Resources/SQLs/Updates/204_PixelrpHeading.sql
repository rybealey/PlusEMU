-- pixelrp: a PixelRP heading at the top of the Builders tab, over the hotel's
-- own pages in a set order.
--
-- The same group label 201 made of Construction: a page whose page_link is
-- 'heading', drawn by the client as its caption, padded and in capitals, over
-- the categories after it (CatalogNavigationItemView). The tab now reads
--
--     PIXELRP
--     Information
--     Roomad
--     Infrastructure
--     Corporations
--     Turfs
--     Builders Club
--     ...anything else that stood above Construction, in its old order
--     CONSTRUCTION
--     Customs
--     Designer
--     ...
--
-- Disabled (enabled = 0) like every heading and divider: the index sends it
-- with no page id, so it cannot be opened and search passes over it.
--
-- These pages exist in no migration (Roomad and Turfs aside), so they are found
-- by caption directly under Builders, as 102 and 161 found them; one that is
-- not there is skipped. Then the whole tab is renumbered 1..n - the pages above
-- first, everything else after them in the order it already had.
--
-- Idempotent: fixed id, and renumbering an ordered tab changes nothing.

SET @builders := (SELECT `id` FROM `catalog_pages` WHERE `parent_id` = -1 AND `caption` = 'Builders' LIMIT 1);

INSERT IGNORE INTO `catalog_pages`
    (`id`,`parent_id`,`caption`,`icon_image`,`min_rank`,`min_vip`,`order_num`,`page_link`,
     `page_layout`,`page_strings_1`,`page_strings_2`,`visible`,`enabled`)
SELECT 956000, @builders, 'PixelRP', 0, 2, 0, 0, 'heading', 'default_3x3', '', '', b'1', b'0'
  FROM DUAL WHERE @builders IS NOT NULL;

DROP TABLE IF EXISTS `_ph_top`;
CREATE TABLE `_ph_top` (`id` INT NOT NULL PRIMARY KEY, `pos` INT NOT NULL) ENGINE=InnoDB;
INSERT IGNORE INTO `_ph_top` (`id`, `pos`)
    SELECT `id`, 0 FROM `catalog_pages` WHERE `id` = 956000 AND `parent_id` = @builders;
INSERT IGNORE INTO `_ph_top` (`id`, `pos`)
    SELECT `id`, 1 FROM `catalog_pages` WHERE `parent_id` = @builders AND `caption` = 'Information' ORDER BY `id` LIMIT 1;
INSERT IGNORE INTO `_ph_top` (`id`, `pos`)
    SELECT `id`, 2 FROM `catalog_pages` WHERE `parent_id` = @builders AND `caption` = 'Roomad' ORDER BY `id` LIMIT 1;
INSERT IGNORE INTO `_ph_top` (`id`, `pos`)
    SELECT `id`, 3 FROM `catalog_pages` WHERE `parent_id` = @builders AND `caption` = 'Infrastructure' ORDER BY `id` LIMIT 1;
INSERT IGNORE INTO `_ph_top` (`id`, `pos`)
    SELECT `id`, 4 FROM `catalog_pages` WHERE `parent_id` = @builders AND `caption` = 'Corporations' ORDER BY `id` LIMIT 1;
INSERT IGNORE INTO `_ph_top` (`id`, `pos`)
    SELECT `id`, 5 FROM `catalog_pages` WHERE `parent_id` = @builders AND `caption` = 'Turfs' ORDER BY `id` LIMIT 1;
INSERT IGNORE INTO `_ph_top` (`id`, `pos`)
    SELECT `id`, 6 FROM `catalog_pages` WHERE `parent_id` = @builders AND `caption` = 'Builders Club' ORDER BY `id` LIMIT 1;

-- ROW_NUMBER keeps the derived table materialised, so MySQL lets it read the
-- table being updated (95 does the same).
UPDATE `catalog_pages` p
  JOIN (
      SELECT c.`id`,
             ROW_NUMBER() OVER (ORDER BY COALESCE(t.`pos`, 100), c.`order_num`, c.`id`) AS `rn`
        FROM `catalog_pages` c
        LEFT JOIN `_ph_top` t ON t.`id` = c.`id`
       WHERE c.`parent_id` = @builders
  ) o ON o.`id` = p.`id`
   SET p.`order_num` = o.`rn`;

DROP TABLE `_ph_top`;

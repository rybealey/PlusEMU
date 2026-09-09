-- PixelRP: put the catalog's subcategories in alphabetical order.
--
-- Two places had drifted. The three Builders custom categories were built by
-- four migrations over two nights (84 Kasja, 89 Designer Furni, 92 designers,
-- 93/94 Construction and Customs), each numbering its own pages from 1 in the
-- order it happened to add them - so Kasja's packs read Modern Cabin, Autumn,
-- Beach House, and the packs 92 appended sat below the ones 84 had. And Furni
-- By Line got Chill Modern appended at the end (88), because the default
-- catalog's line order is not alphabetical and there was no slot to insert
-- into.
--
-- This renumbers CHILDREN, never top-level tabs and never the Builders tab's
-- own groups: Navigation, the dividers, Cartier, Club and the three custom
-- categories keep the order they were given, which is deliberate and not
-- alphabetical.
--
-- Idempotent by construction - sorting an already sorted list changes nothing -
-- and safe to re-run after another import adds pages.

-- 1. The parents whose children get sorted: the three custom categories, Furni
--    By Line, and everything underneath them. A leaf ends up in here too, which
--    only means it has no children to renumber.
SET @builders := (SELECT `id` FROM `catalog_pages` WHERE `parent_id` = -1 AND `caption` = 'Builders' LIMIT 1);
SET @furni := (SELECT `id` FROM `catalog_pages` WHERE `parent_id` = -1 AND `page_link` = 'furni' LIMIT 1);

DROP TABLE IF EXISTS `_alpha_parents`;
CREATE TABLE `_alpha_parents` (`id` INT NOT NULL PRIMARY KEY) ENGINE=InnoDB;

INSERT IGNORE INTO `_alpha_parents` (`id`)
    SELECT `id` FROM `catalog_pages`
    WHERE (`parent_id` = @builders AND `caption` IN ('Construction', 'Customs', 'Designer'))
       OR (`parent_id` = @furni AND `caption` = 'Furni By Line');

-- Descend one level per statement. The deepest of these is Designer > designer
-- > pack > sub-pack, so four passes is a level of slack.
INSERT IGNORE INTO `_alpha_parents` (`id`)
    SELECT c.`id` FROM `catalog_pages` c JOIN `_alpha_parents` p ON c.`parent_id` = p.`id`;
INSERT IGNORE INTO `_alpha_parents` (`id`)
    SELECT c.`id` FROM `catalog_pages` c JOIN `_alpha_parents` p ON c.`parent_id` = p.`id`;
INSERT IGNORE INTO `_alpha_parents` (`id`)
    SELECT c.`id` FROM `catalog_pages` c JOIN `_alpha_parents` p ON c.`parent_id` = p.`id`;
INSERT IGNORE INTO `_alpha_parents` (`id`)
    SELECT c.`id` FROM `catalog_pages` c JOIN `_alpha_parents` p ON c.`parent_id` = p.`id`;

-- 2. Renumber each of those parents' children 1..n by caption. ROW_NUMBER is a
--    MySQL 8 window function; the hotel runs 8.0 (compose.yaml).
UPDATE `catalog_pages` p
    JOIN (
        SELECT c.`id`,
               ROW_NUMBER() OVER (PARTITION BY c.`parent_id` ORDER BY c.`caption`) AS `rn`
        FROM `catalog_pages` c
        JOIN `_alpha_parents` a ON c.`parent_id` = a.`id`
    ) o ON o.`id` = p.`id`
    SET p.`order_num` = o.`rn`;

DROP TABLE `_alpha_parents`;

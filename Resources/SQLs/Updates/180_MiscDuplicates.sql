-- pixelrp: Furni > Misc loses its duplicate furni and its empty categories.
--
-- Misc is 119's catch-all: 'More Furni' and its 27 letter pages (A-Z, and '#'
-- for everything else), holding every furniture row no other page sold. Many
-- of those rows are a second furniture row for a furni the shop already sells
-- - the base dump and 31's official library often define the same furni under
-- two classnames (7957 'cland15_unicornpoo' and 1000000674 'cland15_unipoo',
-- both Unicorn Praline) - and the copy nobody placed fell into Misc.
--
-- A DUPLICATE IS THE SAME TYPE AND SPRITE, NOT THE SAME ID. The client draws a
-- furni by its sprite, so two rows with one sprite are one furni to a player.
-- Floor and wall sprites are separate numbers, so the type is part of it.
--
-- 1. A Misc row goes when its furni is sold on an open page (visible and
--    enabled) anywhere outside Misc. Staff and Builders pages count, so a free
--    copy of a staff-only rare leaves Misc too.
-- 2. Within Misc, one row is kept per furni: the one with a real name (a
--    public_name that is not blank and is not just the classname), then the
--    oldest row.
-- 3. A page under Misc with nothing on it and nothing under it is deleted -
--    letter pages first, then More Furni if all of its letters went. Misc
--    itself stays.
--
-- Only rows on pages under Misc are ever deleted. Idempotent: a second run
-- finds no duplicates and no empty pages.

SET @furni := (SELECT `id` FROM `catalog_pages` WHERE `parent_id` = -1 AND `page_link` = 'furni' LIMIT 1);
SET @misc := (SELECT `id` FROM `catalog_pages` WHERE `parent_id` = @furni AND `caption` = 'Misc' LIMIT 1);

-- The pages under Misc, three levels down (Misc > More Furni > letter), with a
-- level of slack.
DROP TABLE IF EXISTS `_misc_pages`;
CREATE TABLE `_misc_pages` (`id` INT NOT NULL PRIMARY KEY) ENGINE=InnoDB;
INSERT IGNORE INTO `_misc_pages` (`id`) SELECT `id` FROM `catalog_pages` WHERE `parent_id` = @misc;
INSERT IGNORE INTO `_misc_pages` (`id`)
    SELECT c.`id` FROM `catalog_pages` c JOIN `_misc_pages` p ON c.`parent_id` = p.`id`;
INSERT IGNORE INTO `_misc_pages` (`id`)
    SELECT c.`id` FROM `catalog_pages` c JOIN `_misc_pages` p ON c.`parent_id` = p.`id`;
INSERT IGNORE INTO `_misc_pages` (`id`)
    SELECT c.`id` FROM `catalog_pages` c JOIN `_misc_pages` p ON c.`parent_id` = p.`id`;

-- 1. Every furni (type + sprite) sold on an open page outside Misc.
DROP TABLE IF EXISTS `_misc_sold_elsewhere`;
CREATE TABLE `_misc_sold_elsewhere` (
    `type` VARCHAR(2) NOT NULL, `sprite_id` INT NOT NULL,
    PRIMARY KEY (`type`, `sprite_id`)) ENGINE=InnoDB;
INSERT IGNORE INTO `_misc_sold_elsewhere` (`type`, `sprite_id`)
    SELECT f.`type`, f.`sprite_id`
      FROM `catalog_items` ci
      JOIN `catalog_pages` p ON p.`id` = ci.`page_id`
      JOIN `furniture` f ON CAST(f.`id` AS CHAR) = ci.`item_id`
     WHERE p.`visible` = b'1' AND p.`enabled` = b'1'
       AND p.`id` <> @misc
       AND p.`id` NOT IN (SELECT `id` FROM `_misc_pages`);

DELETE ci FROM `catalog_items` ci
  JOIN `_misc_pages` m ON m.`id` = ci.`page_id`
  JOIN `furniture` f ON CAST(f.`id` AS CHAR) = ci.`item_id`
  JOIN `_misc_sold_elsewhere` e ON e.`type` = f.`type` AND e.`sprite_id` = f.`sprite_id`;

-- 2. Within Misc, the rows after the first for each furni.
DROP TABLE IF EXISTS `_misc_extra`;
CREATE TABLE `_misc_extra` (`id` INT NOT NULL PRIMARY KEY) ENGINE=InnoDB;
INSERT INTO `_misc_extra` (`id`)
    SELECT r.`id` FROM (
        SELECT ci.`id`,
               ROW_NUMBER() OVER (
                   PARTITION BY f.`type`, f.`sprite_id`
                   ORDER BY (f.`public_name` = '' OR f.`public_name` = f.`item_name`), ci.`id`) AS `n`
          FROM `catalog_items` ci
          JOIN `_misc_pages` m ON m.`id` = ci.`page_id`
          JOIN `furniture` f ON CAST(f.`id` AS CHAR) = ci.`item_id`) r
     WHERE r.`n` > 1;

DELETE ci FROM `catalog_items` ci JOIN `_misc_extra` x ON x.`id` = ci.`id`;

-- 3. Empty pages under Misc: letters, then More Furni once its letters are gone.
DELETE p FROM `catalog_pages` p
  JOIN `_misc_pages` m ON m.`id` = p.`id`
 WHERE NOT EXISTS (SELECT 1 FROM `catalog_items` ci WHERE ci.`page_id` = p.`id`)
   AND NOT EXISTS (SELECT 1 FROM (SELECT `parent_id` FROM `catalog_pages`) c WHERE c.`parent_id` = p.`id`);
DELETE p FROM `catalog_pages` p
  JOIN `_misc_pages` m ON m.`id` = p.`id`
 WHERE NOT EXISTS (SELECT 1 FROM `catalog_items` ci WHERE ci.`page_id` = p.`id`)
   AND NOT EXISTS (SELECT 1 FROM (SELECT `parent_id` FROM `catalog_pages`) c WHERE c.`parent_id` = p.`id`);

DROP TABLE `_misc_extra`;
DROP TABLE `_misc_sold_elsewhere`;
DROP TABLE `_misc_pages`;

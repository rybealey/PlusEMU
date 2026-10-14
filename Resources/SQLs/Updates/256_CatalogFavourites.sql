-- pixelrp: Favourites in the furni shop - every player's own list of shop
-- categories, starred from the category list (RpCatalogFavouriteEvent).
--
-- 1. rp_catalog_favourites holds the list, one row per category, per ACCOUNT
--    (users.parent_id ?? users.id): every character of one person shares it.
--
-- 2. Two Favourites categories show it. Both are the same page link,
--    'favourites', which the client draws as the list rather than a page of
--    furni; opening one shows the text below.
--      * Builders > PixelRP, at the bottom, right after Corporations (961000).
--      * the top of the Furni tab (961001), for players below rank 2, who
--        never see the Builders tab.
--    Both wear the gold star (195)...
--
-- 3. ...which Customs wore (214), so Customs takes icon 194.
--
-- Idempotent: fixed ids, INSERT IGNORE, and placing by ROW_NUMBER puts each
-- Favourites back where it already is.

CREATE TABLE IF NOT EXISTS `rp_catalog_favourites` (
  `account_id` int NOT NULL,
  `page_id` int NOT NULL,
  `created_at` timestamp NOT NULL DEFAULT CURRENT_TIMESTAMP,
  PRIMARY KEY (`account_id`, `page_id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

SET @builders := (SELECT `id` FROM `catalog_pages` WHERE `parent_id` = -1 AND `caption` = 'Builders' LIMIT 1);
SET @furni := (SELECT `id` FROM `catalog_pages` WHERE `parent_id` = -1 AND `caption` = 'Furni' LIMIT 1);
SET @favourites_text := 'Your favourite shop categories, all in one place. Hover over any category and click its star to add it here. Click a gold star to take it off.';

INSERT IGNORE INTO `catalog_pages`
    (`id`,`parent_id`,`caption`,`icon_image`,`min_rank`,`min_vip`,`order_num`,`page_link`,
     `page_layout`,`page_strings_1`,`page_strings_2`,`visible`,`enabled`)
SELECT 961000, @builders, 'Favourites', 195, 1, 0, 0, 'favourites', 'default_3x3', '', @favourites_text, b'1', b'1'
  FROM DUAL WHERE @builders IS NOT NULL;

INSERT IGNORE INTO `catalog_pages`
    (`id`,`parent_id`,`caption`,`icon_image`,`min_rank`,`min_vip`,`order_num`,`page_link`,
     `page_layout`,`page_strings_1`,`page_strings_2`,`visible`,`enabled`)
SELECT 961001, @furni, 'Favourites', 195, 1, 0, 0, 'favourites', 'default_3x3', '', @favourites_text, b'1', b'1'
  FROM DUAL WHERE @furni IS NOT NULL;

-- Builders: straight after Corporations, the last of the PixelRP group. With
-- no Corporations, at the end of the tab.
SET @corporations_order := COALESCE(
    (SELECT `order_num` FROM `catalog_pages` WHERE `parent_id` = @builders AND `caption` = 'Corporations' ORDER BY `id` LIMIT 1),
    1000000);

UPDATE `catalog_pages` p
  JOIN (
      SELECT c.`id`,
             ROW_NUMBER() OVER (ORDER BY
                 CASE WHEN c.`id` = 961000 THEN @corporations_order ELSE c.`order_num` END,
                 CASE WHEN c.`id` = 961000 THEN 1 ELSE 0 END,
                 c.`order_num`, c.`id`) AS `rn`
        FROM `catalog_pages` c
       WHERE c.`parent_id` = @builders
  ) o ON o.`id` = p.`id`
   SET p.`order_num` = o.`rn`
 WHERE @builders IS NOT NULL;

-- Furni: first.
UPDATE `catalog_pages` p
  JOIN (
      SELECT c.`id`,
             ROW_NUMBER() OVER (ORDER BY
                 CASE WHEN c.`id` = 961001 THEN 0 ELSE 1 END,
                 c.`order_num`, c.`id`) AS `rn`
        FROM `catalog_pages` c
       WHERE c.`parent_id` = @furni
  ) o ON o.`id` = p.`id`
   SET p.`order_num` = o.`rn`
 WHERE @furni IS NOT NULL;

-- Customs gives the star up.
SET @customs := COALESCE(
    (SELECT `id` FROM `catalog_pages` WHERE `id` = 944000 AND `parent_id` = @builders LIMIT 1),
    (SELECT `id` FROM `catalog_pages` WHERE `parent_id` = @builders AND `caption` = 'Customs' ORDER BY `id` LIMIT 1));
UPDATE `catalog_pages` SET `icon_image` = 194 WHERE `id` = @customs;

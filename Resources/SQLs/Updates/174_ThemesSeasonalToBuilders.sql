-- pixelrp: Themes and Seasonal move from the Furni tab to the Builders tab.
--
-- They are 119's taxonomy categories (every furniline outside the curated
-- Lines, and the holiday ranges), found by caption under Furni. They go to the
-- end of Builders as their own group, with a divider above them the way 84 set
-- one above Kasja: the same non-clickable sentinel, page_link = 'divider' and
-- enabled = 0.
--
-- Builders is rank 2, and a page is only shown when every page above it lets
-- the player in, so rank 1 players stop seeing both. Their own min_rank, and
-- everything underneath them, is left as it is.
--
-- Idempotent: once they are under Builders they are no longer found under
-- Furni, so a re-run moves nothing and adds no second divider (fixed id).

SET @furni := (SELECT `id` FROM `catalog_pages` WHERE `parent_id` = -1 AND `page_link` = 'furni' LIMIT 1);
SET @builders := (SELECT `id` FROM `catalog_pages` WHERE `parent_id` = -1 AND `caption` = 'Builders' LIMIT 1);
SET @themes := (SELECT `id` FROM `catalog_pages` WHERE `parent_id` = @furni AND `caption` = 'Themes' LIMIT 1);
SET @seasonal := (SELECT `id` FROM `catalog_pages` WHERE `parent_id` = @furni AND `caption` = 'Seasonal' LIMIT 1);
SET @last := (SELECT COALESCE(MAX(`order_num`), 0) FROM `catalog_pages` WHERE `parent_id` = @builders);

INSERT IGNORE INTO `catalog_pages`
    (`id`,`parent_id`,`caption`,`icon_image`,`min_rank`,`min_vip`,`order_num`,`page_link`,
     `page_layout`,`page_strings_1`,`page_strings_2`,`visible`,`enabled`)
SELECT 953000, @builders, '-', 0, 2, 0, @last + 1, 'divider', 'default_3x3', '', '', b'1', b'0'
  FROM DUAL
 WHERE @builders IS NOT NULL AND (@themes IS NOT NULL OR @seasonal IS NOT NULL);

UPDATE `catalog_pages` SET `parent_id` = @builders, `order_num` = @last + 2
 WHERE `id` = @themes AND @builders IS NOT NULL;

UPDATE `catalog_pages` SET `parent_id` = @builders, `order_num` = @last + 3
 WHERE `id` = @seasonal AND @builders IS NOT NULL;

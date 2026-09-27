-- pixelrp: Lines is in the Builders tab too, first under the Habbo heading, and
-- stays where it is in the Furni tab.
--
-- A page has one parent, so this is not a second copy: it is a mirror, a page
-- whose page_link is 'mirror' and whose page_strings_1 names the page it shows
-- (CatalogLookup.MirrorLink). CatalogIndexComposer writes it as Furni's Lines
-- branch - the same page ids, the same furni - under the mirror's caption and
-- icon, so opening Lines from either tab loads the one real page, and anything
-- done to Lines later shows in both. Builders' search looks inside it too.
--
--     HABBO
--     Lines        <- Furni > Lines, mirrored
--     Themes
--     Seasonal
--
-- The mirror row itself is disabled - it has no furni and is never opened, the
-- index only ever sends the page it shows. Lines' own rank (1) still applies
-- to what is inside it; Builders is rank 2, so here it shows from rank 2.
--
-- Idempotent: fixed id, refreshed to point at Lines on a re-run, and the order
-- is renumbered rather than shifted.

SET @builders := (SELECT `id` FROM `catalog_pages` WHERE `parent_id` = -1 AND `caption` = 'Builders' LIMIT 1);
SET @furni := (SELECT `id` FROM `catalog_pages` WHERE `parent_id` = -1 AND `page_link` = 'furni' LIMIT 1);
SET @lines := (SELECT `id` FROM `catalog_pages` WHERE `parent_id` = @furni AND `caption` = 'Lines' ORDER BY `id` LIMIT 1);
SET @lines_icon := (SELECT `icon_image` FROM `catalog_pages` WHERE `id` = @lines);
SET @habbo := (SELECT `id` FROM `catalog_pages` WHERE `id` = 953000 AND `parent_id` = @builders LIMIT 1);
SET @last := (SELECT COALESCE(MAX(`order_num`), 0) FROM `catalog_pages` WHERE `parent_id` = @builders);

INSERT IGNORE INTO `catalog_pages`
    (`id`,`parent_id`,`caption`,`icon_image`,`min_rank`,`min_vip`,`order_num`,`page_link`,
     `page_layout`,`page_strings_1`,`page_strings_2`,`visible`,`enabled`)
SELECT 958000, @builders, 'Lines', COALESCE(@lines_icon, 1), 1, 0, @last + 1, 'mirror', 'default_3x3', CAST(@lines AS CHAR), '', b'1', b'0'
  FROM DUAL WHERE @builders IS NOT NULL AND @lines IS NOT NULL;

UPDATE `catalog_pages`
   SET `page_strings_1` = CAST(@lines AS CHAR), `icon_image` = COALESCE(@lines_icon, `icon_image`), `page_link` = 'mirror'
 WHERE `id` = 958000 AND @lines IS NOT NULL;

-- Straight after the Habbo heading; everything else keeps the order it has.
-- Without the heading it stays at the end of the tab, where it went in.
SET @habbo_order := (SELECT `order_num` FROM `catalog_pages` WHERE `id` = @habbo);

UPDATE `catalog_pages` p
  JOIN (
      SELECT c.`id`,
             ROW_NUMBER() OVER (ORDER BY
                 CASE WHEN c.`id` = 958000 THEN @habbo_order ELSE c.`order_num` END,
                 CASE c.`id` WHEN 953000 THEN 0 WHEN 958000 THEN 1 ELSE 2 END,
                 c.`id`) AS `rn`
        FROM `catalog_pages` c
       WHERE c.`parent_id` = @builders
  ) o ON o.`id` = p.`id`
   SET p.`order_num` = o.`rn`
 WHERE @habbo_order IS NOT NULL;

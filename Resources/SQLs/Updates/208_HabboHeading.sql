-- pixelrp: Themes and Seasonal sit under a Habbo heading in the Builders tab.
--
-- 174 brought them over from the Furni tab to the end of Builders, with a
-- divider (953000) above them. That divider becomes a heading, as 201 made one
-- of Construction: page_link 'heading', drawn by the client as its caption,
-- padded and in capitals, over the categories after it. So the tab ends
--
--     CONSTRUCTION
--     Customs
--     Designer
--     HABBO
--     Themes
--     Seasonal
--
-- Still disabled (enabled = 0), as the divider was: it has no page id, so it
-- cannot be opened and search passes over it. Should the divider be missing it
-- is made, with the same id. The three go to the end of the tab in that order;
-- everything else keeps the order it has.
--
-- Idempotent: fixed id, and a re-run finds the heading and the order in place.

SET @builders := (SELECT `id` FROM `catalog_pages` WHERE `parent_id` = -1 AND `caption` = 'Builders' LIMIT 1);
SET @themes := (SELECT `id` FROM `catalog_pages` WHERE `parent_id` = @builders AND `caption` = 'Themes' ORDER BY `id` LIMIT 1);
SET @seasonal := (SELECT `id` FROM `catalog_pages` WHERE `parent_id` = @builders AND `caption` = 'Seasonal' ORDER BY `id` LIMIT 1);

INSERT IGNORE INTO `catalog_pages`
    (`id`,`parent_id`,`caption`,`icon_image`,`min_rank`,`min_vip`,`order_num`,`page_link`,
     `page_layout`,`page_strings_1`,`page_strings_2`,`visible`,`enabled`)
SELECT 953000, @builders, 'Habbo', 0, 2, 0, 0, 'heading', 'default_3x3', '', '', b'1', b'0'
  FROM DUAL
 WHERE @builders IS NOT NULL AND (@themes IS NOT NULL OR @seasonal IS NOT NULL);

UPDATE `catalog_pages`
   SET `caption` = 'Habbo', `page_link` = 'heading', `enabled` = b'0', `visible` = b'1'
 WHERE `id` = 953000 AND `parent_id` = @builders;

-- ROW_NUMBER keeps the derived table materialised, so MySQL lets it read the
-- table being updated (95 does the same).
UPDATE `catalog_pages` p
  JOIN (
      SELECT c.`id`,
             ROW_NUMBER() OVER (ORDER BY
                 CASE c.`id` WHEN 953000 THEN 1 WHEN @themes THEN 2 WHEN @seasonal THEN 3 ELSE 0 END,
                 c.`order_num`, c.`id`) AS `rn`
        FROM `catalog_pages` c
       WHERE c.`parent_id` = @builders
  ) o ON o.`id` = p.`id`
   SET p.`order_num` = o.`rn`;

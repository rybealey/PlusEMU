-- pixelrp: Builders > Construction > Tiles is one page, not two.
--
-- 93 split Tiles into Cinema Tiles (943071: the six light-up tiles) and
-- Design Tiles (943072: the marble, lava and matte ones). All 17 pieces move
-- onto Tiles itself (943070), Cinema's first as 93 inserted them, and the two
-- emptied pages go.
--
-- Idempotent: a second run finds nothing on those pages and no pages to
-- delete. Nothing happens if Tiles is not there.

SET @tiles := (SELECT `id` FROM `catalog_pages` WHERE `id` = 943070 AND `caption` = 'Tiles' LIMIT 1);

UPDATE `catalog_items` SET `page_id` = @tiles
 WHERE `page_id` IN (943071, 943072) AND @tiles IS NOT NULL;

DELETE p FROM `catalog_pages` p
 WHERE p.`id` IN (943071, 943072) AND p.`parent_id` = 943070 AND @tiles IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM `catalog_items` ci WHERE ci.`page_id` = p.`id`)
   AND NOT EXISTS (SELECT 1 FROM (SELECT `parent_id` FROM `catalog_pages`) c WHERE c.`parent_id` = p.`id`);

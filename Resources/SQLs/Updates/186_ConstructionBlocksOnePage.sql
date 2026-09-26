-- pixelrp: Builders > Construction > Blocks is one page, not 17.
--
-- 93 gave every block material its own page under Blocks (943030-943046:
-- Agua, Arena, Art Deco, Brick, Caja de Metal, Crystal, Grass, Industrial,
-- Lava, Marble, Metal, Plain Wood, Plaqueta, Seto Floral, Stone, Terra, Wool),
-- one piece each. All 17 pieces move onto Blocks itself (943029), in the order
-- 93 inserted them, and the emptied pages go.
--
-- Idempotent: a second run finds nothing on those pages and no pages to
-- delete. Nothing happens if Blocks is not there.

SET @blocks := (SELECT `id` FROM `catalog_pages` WHERE `id` = 943029 AND `caption` = 'Blocks' LIMIT 1);

UPDATE `catalog_items` SET `page_id` = @blocks
 WHERE `page_id` BETWEEN 943030 AND 943046 AND @blocks IS NOT NULL;

DELETE p FROM `catalog_pages` p
 WHERE p.`id` BETWEEN 943030 AND 943046 AND p.`parent_id` = 943029 AND @blocks IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM `catalog_items` ci WHERE ci.`page_id` = p.`id`)
   AND NOT EXISTS (SELECT 1 FROM (SELECT `parent_id` FROM `catalog_pages`) c WHERE c.`parent_id` = p.`id`);

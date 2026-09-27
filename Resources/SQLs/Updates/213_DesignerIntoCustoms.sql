-- pixelrp: Builders > Designer's categories move into Customs, and Customs is
-- sorted A to Z.
--
-- Designer (941000) held eighteen categories after 199 and 200 - Kitchen,
-- Bathroom, Living Room & Bedroom and the rest by type, and Cheapyxony - each
-- with its themed pages under it. They all become sets of Customs, pages and
-- furni untouched, so the Construction group reads Builders Club, Customs.
-- Customs keeps its sets alphabetical (193, 201, 207); the eighteen are sorted
-- in among its own. None shares a caption with a Customs set, though some sit
-- side by side - Beach and Beach & Water, Building and Building & Structure.
--
-- Designer itself goes once it is empty, the way 199 removed the pages it
-- emptied; if anything is still sold on it, it stays.
--
-- Idempotent: a re-run finds nothing under Designer, and sorting a sorted
-- list changes nothing.

SET @builders := (SELECT `id` FROM `catalog_pages` WHERE `parent_id` = -1 AND `caption` = 'Builders' LIMIT 1);
SET @customs := COALESCE(
    (SELECT `id` FROM `catalog_pages` WHERE `id` = 944000 AND `parent_id` = @builders LIMIT 1),
    (SELECT `id` FROM `catalog_pages` WHERE `parent_id` = @builders AND `caption` = 'Customs' ORDER BY `id` LIMIT 1));
SET @designer := COALESCE(
    (SELECT `id` FROM `catalog_pages` WHERE `id` = 941000 AND `parent_id` = @builders LIMIT 1),
    (SELECT `id` FROM `catalog_pages` WHERE `parent_id` = @builders AND `caption` IN ('Designer', 'Designer Furni') ORDER BY `id` LIMIT 1));

UPDATE `catalog_pages` SET `parent_id` = @customs
 WHERE `parent_id` = @designer AND @designer IS NOT NULL AND @customs IS NOT NULL;

SET @n := 0;
UPDATE `catalog_pages` SET `order_num` = (@n := @n + 1)
 WHERE `parent_id` = @customs AND @customs IS NOT NULL
 ORDER BY `caption`, `id`;

DELETE p FROM `catalog_pages` p
 WHERE p.`id` = @designer AND @customs IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM `catalog_items` ci WHERE ci.`page_id` = p.`id`)
   AND NOT EXISTS (SELECT 1 FROM (SELECT `parent_id` FROM `catalog_pages`) c WHERE c.`parent_id` = p.`id`);

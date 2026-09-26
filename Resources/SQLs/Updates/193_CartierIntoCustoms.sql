-- pixelrp: Builders > Cartier moves into Builders > Customs.
--
-- Cartier (912375, 59) becomes one of the Customs sets, which are kept
-- alphabetical (166's epilogue) - Customs is renumbered after. Its rank stays
-- as it is: both are Builders pages.
--
-- 59 planted a divider (912374) above Cartier to set Cartier and Club apart
-- from the tab's first group. With Cartier here and Club gone to Furni >
-- Lines (190), it would sit directly on top of the divider 84 put above Kasja
-- - two lines in a row - so it goes too, once neither is left beside it.
--
-- Idempotent: once Cartier is under Customs it is no longer under Builders.

SET @builders := (SELECT `id` FROM `catalog_pages` WHERE `parent_id` = -1 AND `caption` = 'Builders' LIMIT 1);
SET @customs := COALESCE(
    (SELECT `id` FROM `catalog_pages` WHERE `id` = 944000 AND `parent_id` = @builders LIMIT 1),
    (SELECT `id` FROM `catalog_pages` WHERE `parent_id` = @builders AND `caption` = 'Customs' ORDER BY `id` LIMIT 1));
SET @cartier := (SELECT `id` FROM `catalog_pages` WHERE `parent_id` = @builders AND `caption` = 'Cartier' ORDER BY `id` LIMIT 1);

UPDATE `catalog_pages` SET `parent_id` = @customs WHERE `id` = @cartier AND @customs IS NOT NULL;

SET @n := 0;
UPDATE `catalog_pages` SET `order_num` = (@n := @n + 1)
 WHERE `parent_id` = @customs AND @customs IS NOT NULL
 ORDER BY `caption`;

-- the divider that stood above Cartier and Club, now that neither is here
DELETE FROM `catalog_pages`
 WHERE `id` = 912374 AND `page_link` = 'divider'
   AND NOT EXISTS (SELECT 1 FROM (SELECT `id`, `parent_id`, `caption` FROM `catalog_pages`) c
                    WHERE c.`parent_id` = @builders AND c.`caption` IN ('Cartier', 'Club'));

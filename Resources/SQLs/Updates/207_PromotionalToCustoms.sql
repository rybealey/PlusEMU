-- pixelrp: Promotional moves from the Furni tab into Builders > Customs.
--
-- It is the page 182 made for the ads_ furni it took out of Misc (953200),
-- which sat just above Misc on the Furni tab, open to everyone. Customs keeps
-- its sets alphabetical (193, 201), so it is re-sorted after the move.
--
-- Builders is rank 2, and a page is only shown when every page above it lets
-- the player in, so rank 1 players stop seeing it. Its own min_rank, and its
-- icon (stock 6), are left as they are.
--
-- Found by id first, caption second, the way 182 made it.
-- Idempotent: once it is under Customs nothing moves, and sorting a sorted
-- list changes nothing.

SET @builders := (SELECT `id` FROM `catalog_pages` WHERE `parent_id` = -1 AND `caption` = 'Builders' LIMIT 1);
SET @furni := (SELECT `id` FROM `catalog_pages` WHERE `parent_id` = -1 AND `page_link` = 'furni' LIMIT 1);
SET @customs := COALESCE(
    (SELECT `id` FROM `catalog_pages` WHERE `id` = 944000 AND `parent_id` = @builders LIMIT 1),
    (SELECT `id` FROM `catalog_pages` WHERE `parent_id` = @builders AND `caption` = 'Customs' ORDER BY `id` LIMIT 1));
SET @promotional := COALESCE(
    (SELECT `id` FROM `catalog_pages` WHERE `id` = 953200 AND `caption` = 'Promotional' LIMIT 1),
    (SELECT `id` FROM `catalog_pages` WHERE `parent_id` = @furni AND `caption` = 'Promotional' ORDER BY `id` LIMIT 1));

UPDATE `catalog_pages` SET `parent_id` = @customs
 WHERE `id` = @promotional AND @customs IS NOT NULL AND `parent_id` <> @customs;

SET @n := 0;
UPDATE `catalog_pages` SET `order_num` = (@n := @n + 1)
 WHERE `parent_id` = @customs AND @customs IS NOT NULL
 ORDER BY `caption`;

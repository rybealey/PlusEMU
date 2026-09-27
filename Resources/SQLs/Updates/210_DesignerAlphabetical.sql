-- pixelrp: Builders > Designer's categories in alphabetical order.
--
-- 199 numbered its seventeen type categories in an order of its own (Kitchen,
-- Bathroom, Living Room & Bedroom...) and 200 put Cheapyxony after them. Now
-- they read A to Z, Cheapyxony among them, the way Customs' sets do (193, 201):
--
--     Aesthetic & Pastel, Asian, Bathroom, Beach & Water, Building & Structure,
--     Cheapyxony, Cottage & Country, Decoration & Art, Fantasy & Medieval,
--     Garden & Nature, Greek & Classical, Holidays, Kitchen,
--     Living Room & Bedroom, Modern, Retro, Scandinavian & Cabin,
--     Shops, Cafes & Venues
--
-- Only Designer's own children; the pages inside each category keep 199's
-- order. Idempotent: sorting a sorted list changes nothing.

SET @builders := (SELECT `id` FROM `catalog_pages` WHERE `parent_id` = -1 AND `caption` = 'Builders' LIMIT 1);
SET @designer := COALESCE(
    (SELECT `id` FROM `catalog_pages` WHERE `id` = 941000 LIMIT 1),
    (SELECT `id` FROM `catalog_pages` WHERE `parent_id` = @builders AND `caption` IN ('Designer', 'Designer Furni') ORDER BY `id` LIMIT 1));

SET @n := 0;
UPDATE `catalog_pages` SET `order_num` = (@n := @n + 1)
 WHERE `parent_id` = @designer AND @designer IS NOT NULL
 ORDER BY `caption`, `id`;

-- pixelrp: Builders > Customs wears a gold star.
--
-- 206 gave it stock icon 189, a hand-painted block, when it held only the
-- custom sets. Since 213 it holds Designer's furniture too - everything made
-- for the hotel rather than by Habbo - and the star (stock 195) says that
-- better, bold at 18x18. No migration gives 195 to a page, and every set under
-- Customs wears a furni icon, so none shares it.
--
-- Idempotent.

SET @builders := (SELECT `id` FROM `catalog_pages` WHERE `parent_id` = -1 AND `caption` = 'Builders' LIMIT 1);
SET @customs := COALESCE(
    (SELECT `id` FROM `catalog_pages` WHERE `id` = 944000 AND `parent_id` = @builders LIMIT 1),
    (SELECT `id` FROM `catalog_pages` WHERE `parent_id` = @builders AND `caption` = 'Customs' ORDER BY `id` LIMIT 1));

UPDATE `catalog_pages` SET `icon_image` = 195 WHERE `id` = @customs;

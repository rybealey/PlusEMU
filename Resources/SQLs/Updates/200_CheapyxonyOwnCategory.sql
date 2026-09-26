-- pixelrp: Cheapyxony is a category of its own in Builders > Designer.
--
-- 145 hung Cheapyxony (948000, 309 furni) under the Haaziq designer page, and
-- 199 - built from 92's furni only - never knew of it: it filed Haaziq's own
-- Cheapyxo House set by type but left Haaziq standing, holding just this. Now
-- Cheapyxony sits directly under Designer, after the seventeen type
-- categories, wearing its first furni (Habblet_haaziq_02) as its icon, and
-- the empty Haaziq page goes.
--
-- Pages found by id first, caption second, the way 145 found them.
-- Idempotent: a re-run finds Cheapyxony already in place and no Haaziq.

SET @builders := (SELECT `id` FROM `catalog_pages` WHERE `parent_id` = -1 AND `caption` = 'Builders' LIMIT 1);
SET @designer := COALESCE(
    (SELECT `id` FROM `catalog_pages` WHERE `id` = 941000 LIMIT 1),
    (SELECT `id` FROM `catalog_pages` WHERE `parent_id` = @builders AND `caption` IN ('Designer', 'Designer Furni') ORDER BY `id` LIMIT 1));
SET @haaziq := COALESCE(
    (SELECT `id` FROM `catalog_pages` WHERE `id` = 942036 AND `caption` = 'Haaziq' LIMIT 1),
    (SELECT `id` FROM `catalog_pages` WHERE `parent_id` = @designer AND `caption` = 'Haaziq' ORDER BY `id` LIMIT 1));
SET @cheapyxony := COALESCE(
    (SELECT `id` FROM `catalog_pages` WHERE `id` = 948000 AND `caption` = 'Cheapyxony' LIMIT 1),
    (SELECT `id` FROM `catalog_pages` WHERE `caption` = 'Cheapyxony' AND `parent_id` IN (@designer, @haaziq) ORDER BY `id` LIMIT 1));
SET @last := (SELECT COALESCE(MAX(`order_num`), 0) FROM `catalog_pages`
               WHERE `parent_id` = @designer AND `id` BETWEEN 955000 AND 955099);

UPDATE `catalog_pages`
   SET `parent_id` = @designer, `order_num` = @last + 1, `icon_image` = 7110000
 WHERE `id` = @cheapyxony AND @designer IS NOT NULL;

DELETE p FROM `catalog_pages` p
 WHERE p.`id` = @haaziq
   AND NOT EXISTS (SELECT 1 FROM `catalog_items` ci WHERE ci.`page_id` = p.`id`)
   AND NOT EXISTS (SELECT 1 FROM (SELECT `parent_id` FROM `catalog_pages`) c WHERE c.`parent_id` = p.`id`);

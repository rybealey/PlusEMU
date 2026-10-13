-- PixelRP: Builders > Corporations > Cafe becomes The Muse, after the
-- corporation that runs the city's juice bar, and the Muse Vending Machine
-- moves onto it from Furni > Misc (249).
--
-- The page wears the machine's own icon in place of 212's coffee cup: its
-- furni icon fitted to 18x18 as 197's are, icon_<7000000 + sprite 110706>.png
-- under nitro/overrides/c_images/catalogue/.
--
-- Cafe exists in no migration, so it is found by caption under Corporations,
-- as 212 found it (or as The Muse, if this has run before). A DB without it
-- keeps the machine in Misc rather than losing its offer. Idempotent.

SET @builders := (SELECT `id` FROM `catalog_pages` WHERE `parent_id` = -1 AND `caption` = 'Builders' LIMIT 1);
SET @corps    := (SELECT `id` FROM `catalog_pages` WHERE `parent_id` = @builders AND `caption` = 'Corporations' LIMIT 1);
SET @muse     := (SELECT `id` FROM `catalog_pages`
                   WHERE `parent_id` = @corps AND (`caption` IN ('Cafe', 'The Muse') OR `caption` LIKE 'Caf_')
                   ORDER BY `id` LIMIT 1);

UPDATE `catalog_pages` SET `caption` = 'The Muse', `icon_image` = 7110706 WHERE `id` = @muse;

UPDATE `catalog_items` SET `page_id` = @muse
 WHERE `item_id` = '110706' AND @muse IS NOT NULL;

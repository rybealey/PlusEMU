-- pixelrp: Builders > Corporations' categories get icons that say what they
-- sell.
--
--     Armory     a handgun rack      mafia_c26_gunrack   icon_7018929
--     Cafe       a cup of coffee     coffee_cup          icon_7004447
--     Clothing   a clothes rack      clrack              icon_7003184
--     Hospital   a hospital bed      hosptl_bed          icon_7003590
--     Staff      the Habbo H shield  stock icon 344
--
-- Hospital wore 147's hard hat (193), the Builders icon every new page got.
-- The four furni icons are made as 197's are - the furni's icon fitted to
-- 18x18, named icon_<7000000 + sprite>.png under
-- nitro/overrides/c_images/catalogue/ - from Habbo's own furni, which beta
-- serves; stock icon 344 is already on the server.
--
-- Corporations and its categories exist in no migration (Hospital aside), so
-- they are found by caption under it, as 124 and 134 found Clothing and Cafe;
-- one that is not there is skipped. Idempotent.

SET @builders := (SELECT `id` FROM `catalog_pages` WHERE `parent_id` = -1 AND `caption` = 'Builders' LIMIT 1);
SET @corps    := (SELECT `id` FROM `catalog_pages` WHERE `parent_id` = @builders AND `caption` = 'Corporations' LIMIT 1);

UPDATE `catalog_pages` SET `icon_image` = 7018929
 WHERE `parent_id` = @corps AND `caption` IN ('Armory', 'Armoury');
UPDATE `catalog_pages` SET `icon_image` = 7004447
 WHERE `parent_id` = @corps AND (`caption` = 'Cafe' OR `caption` LIKE 'Caf_');
UPDATE `catalog_pages` SET `icon_image` = 7003184
 WHERE `parent_id` = @corps AND `caption` = 'Clothing';
UPDATE `catalog_pages` SET `icon_image` = 7003590
 WHERE `parent_id` = @corps AND `caption` = 'Hospital';
UPDATE `catalog_pages` SET `icon_image` = 344
 WHERE `parent_id` = @corps AND `caption` = 'Staff';

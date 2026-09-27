-- pixelrp: a category never wears the same icon as one of its sub-pages -
-- Builders > Designer's categories, and Builders Club's Shapes and Materials -
-- and Customs and Designer get icons that say what they hold.
--
-- 199 gave every Designer page its first furni's icon, and each category's
-- first furni is on its first sub-page, so Kitchen and Modern Kitchen showed
-- one icon, Holidays and Christmas another, and so on down the list. 197 did
-- the same in Builders Club: Shapes and Materials hold no furni of their own
-- and took their first sub-page's icon. Now each wears a piece of its own
-- that none of its sub-pages wears, the rule 202 kept for Customs:
--
--   Designer, chosen by eye for the piece that says what the category is -
--   a cooking pot over Kitchen, a hay bale over Cottage & Country, a suit of
--   armour over Fantasy & Medieval, a lifebuoy over Beach & Water, paint
--   brushes over Decoration & Art. New icons, fitted to 18x18 and named
--   after the sprite as 197's are: icon_<7000000 + sprite>.png under
--   nitro/overrides/c_images/catalogue/.
--
--   Builders Club, whose shape and material pages all wear the beige first
--   colour of their set: Shapes a blue sphere (bc_sphere*22), Materials a red
--   metal crate (bc_metalcrate*5). Their icons are 197's.
--
-- And the two categories themselves, which wore Builders' construction
-- stock icons (Designer the hard hat 193, Customs 202's hammer and wrench 79)
-- now wear stock icons that say what they hold: Designer a pencil (311),
-- Customs a hand-painted block (189). Neither is worn by anything under
-- them, which is all furni icons, nor anywhere else in Builders.
--
-- Bathroom, Greek & Classical and Cheapyxony have no sub-pages and keep what
-- they wear. Designer pages by fixed id and only while under Designer;
-- Builders Club's by 182's fixed ids, and only where no sub-page already
-- wears the icon, since some of Shapes' sub-pages exist in no migration.
-- Idempotent.

SET @builders := (SELECT `id` FROM `catalog_pages` WHERE `parent_id` = -1 AND `caption` = 'Builders' LIMIT 1);
SET @designer := COALESCE(
    (SELECT `id` FROM `catalog_pages` WHERE `id` = 941000 LIMIT 1),
    (SELECT `id` FROM `catalog_pages` WHERE `parent_id` = @builders AND `caption` IN ('Designer', 'Designer Furni') ORDER BY `id` LIMIT 1));

DROP TABLE IF EXISTS `_pio_icon`;
CREATE TABLE `_pio_icon` (`page_id` INT NOT NULL PRIMARY KEY, `icon` INT NOT NULL) ENGINE=InnoDB;
INSERT INTO `_pio_icon` (`page_id`, `icon`) VALUES
    (955000, 7103461),
    (955002, 7101845),
    (955003, 7101038),
    (955004, 7100554),
    (955005, 7100046),
    (955006, 7104126),
    (955007, 7100773),
    (955008, 7100219),
    (955009, 7102063),
    (955011, 7103616),
    (955012, 7100074),
    (955013, 7102658),
    (955014, 7103353),
    (955015, 7100805),
    (955016, 7102081);

UPDATE `catalog_pages` p
  JOIN `_pio_icon` i ON i.`page_id` = p.`id`
   SET p.`icon_image` = i.`icon`
 WHERE p.`parent_id` = @designer;

DROP TABLE `_pio_icon`;

UPDATE `catalog_pages` p SET p.`icon_image` = 7007233
 WHERE p.`id` = 953102
   AND NOT EXISTS (SELECT 1 FROM (SELECT `parent_id`, `icon_image` FROM `catalog_pages`) c
                    WHERE c.`parent_id` = 953102 AND c.`icon_image` = 7007233);
UPDATE `catalog_pages` p SET p.`icon_image` = 7005675
 WHERE p.`id` = 953103
   AND NOT EXISTS (SELECT 1 FROM (SELECT `parent_id`, `icon_image` FROM `catalog_pages`) c
                    WHERE c.`parent_id` = 953103 AND c.`icon_image` = 7005675);

SET @customs := COALESCE(
    (SELECT `id` FROM `catalog_pages` WHERE `id` = 944000 AND `parent_id` = @builders LIMIT 1),
    (SELECT `id` FROM `catalog_pages` WHERE `parent_id` = @builders AND `caption` = 'Customs' ORDER BY `id` LIMIT 1));

UPDATE `catalog_pages` SET `icon_image` = 311 WHERE `id` = @designer;
UPDATE `catalog_pages` SET `icon_image` = 189 WHERE `id` = @customs;

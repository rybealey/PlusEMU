-- pixelrp: hat_U_autumnberet2 (figure set 6549), the one live Habbo clothing
-- library with sets the hotel still lacked (2026-10-16 HabboAssets audit,
-- docs/clothing-habboassets-audit.md). From Habbo's 2026-09 build, via
-- habboassets.com's mirror of Habbo's own files. Bundle, FigureMap and
-- FigureData ship in nitro/overrides and emulator/Config/figuredata.xml;
-- this is the shelf.
--
-- Habbo's furnidata has no name for it yet (its clothing_autumnberet2 furni
-- is still "clothing_autumnberet2 name"). The sprite is a two-tone flat cap
-- with a brim, so it is labelled as a newsboy cap, apart from the Classic
-- Newsboy Cap and the first Autumnal Baret (6203).
--
-- Unisex, as the library is tagged (_U_), at 50 credits like the rest of the
-- shelf. Named after its library, as every row is since migration 260. A row
-- that already covers the set is left alone.
INSERT INTO `catalog_clothing` (`clothing_name`, `clothing_parts`, `display_name`, `price`)
SELECT n.`clothing_name`, n.`clothing_parts`, n.`display_name`, n.`price`
  FROM (
  SELECT 'hat_U_autumnberet2' AS `clothing_name`, '6549' AS `clothing_parts`, 'Autumn Newsboy Cap' AS `display_name`, 50 AS `price`
  ) n
 WHERE NOT EXISTS (SELECT 1 FROM `catalog_clothing` c
                    WHERE c.`clothing_name` = n.`clothing_name`
                       OR FIND_IN_SET(n.`clothing_parts`, c.`clothing_parts`) > 0);

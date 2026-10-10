-- PixelRP: three faces that should be owned, not handed to everybody -
-- the sunburnt face (6021 male, 6022 female) and the faceless placeholder
-- head (5033).
--
-- A catalog_clothing row is what makes a set need owning on the server
-- (FigureDataManager.ProcessFigure strips it on look save unless the player
-- owns it; staff are exempt from that for faces). Price 0 keeps them off the
-- Clothing Store shelf - put a price on a row to sell it there. The client
-- hides them the same way through FigureData's `sellable` flag: 5033 already
-- has it, 6021/6022 get it from nitro/overrides/gamedata-override/FigureData.json.
--
-- Whoever is wearing one today keeps it until they next save a look.
INSERT INTO `catalog_clothing` (`clothing_name`, `clothing_parts`, `display_name`, `price`)
SELECT 'face_M_sunburntface', '6021', 'Sunburnt Face', 0 FROM DUAL
WHERE NOT EXISTS (SELECT 1 FROM `catalog_clothing` WHERE `clothing_parts` = '6021');

INSERT INTO `catalog_clothing` (`clothing_name`, `clothing_parts`, `display_name`, `price`)
SELECT 'face_F_sunburntface2', '6022', 'Sunburnt Face', 0 FROM DUAL
WHERE NOT EXISTS (SELECT 1 FROM `catalog_clothing` WHERE `clothing_parts` = '6022');

INSERT INTO `catalog_clothing` (`clothing_name`, `clothing_parts`, `display_name`, `price`)
SELECT 'face_U_placeholder', '5033', 'Faceless', 0 FROM DUAL
WHERE NOT EXISTS (SELECT 1 FROM `catalog_clothing` WHERE `clothing_parts` = '5033');

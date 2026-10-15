-- pixelrp: ten pieces of Habbo clothing the hotel did not have yet, from
-- Habbo's 2026-09/10 releases (via habboassets.com's mirror of Habbo's own
-- files). Bundles, FigureMap and FigureData ship in nitro/overrides and
-- emulator/Config/figuredata.xml; this is the shelf.
--
-- Everything is unisex, as the libraries are tagged (_U_). Habbo's held
-- items (set type mc: cotton candy, cutlery, trash can) are belts here, as
-- every held item before them, keeping their hand parts.
--
-- Eight go on sale at 50 credits like the rest of the shelf. The two faces
-- are sellable but priced 0: the store has no face shelf, so a price-0 row
-- is what makes them owned-only - staff wear them, players are granted them.
-- A set some row already covers is left alone.
INSERT INTO `catalog_clothing` (`clothing_name`, `clothing_parts`, `display_name`, `price`)
SELECT n.`clothing_name`, n.`clothing_parts`, n.`display_name`, n.`price`
  FROM (
  SELECT 'hat_U_bananahelmet' AS `clothing_name`, '6609' AS `clothing_parts`, 'Banana Helmet' AS `display_name`, 50 AS `price`
  UNION ALL
  SELECT 'hat_U_nftbirdnest', '6608', 'Bird''s Nest', 50
  UNION ALL
  SELECT 'hat_U_nfttrashcan', '6622', 'Trash Can Hat', 50
  UNION ALL
  SELECT 'acc_face_U_nftzigzagstache', '6623', 'Zigzag Moustache', 50
  UNION ALL
  SELECT 'acc_head_U_nfthaircomb', '6624', 'Hair Comb', 50
  UNION ALL
  SELECT 'misc_U_nftcottoncandy', '6595', 'Cotton Candy', 50
  UNION ALL
  SELECT 'misc_U_nftcutlery', '6593', 'Cutlery', 50
  UNION ALL
  SELECT 'misc_U_nfttrashcan', '6621', 'Trash Can', 50
  UNION ALL
  SELECT 'face_U_nftdeterminedface', '6591', 'Determined Face', 0
  UNION ALL
  SELECT 'face_U_nfthopefulface', '6592', 'Hopeful Face', 0
  ) n
 WHERE NOT EXISTS (SELECT 1 FROM `catalog_clothing` c
                    WHERE c.`clothing_name` = n.`clothing_name`
                       OR FIND_IN_SET(n.`clothing_parts`, c.`clothing_parts`) > 0);

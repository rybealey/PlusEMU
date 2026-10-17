-- pixelrp: more of the Wulez clothing pack (262), added by a later run of
-- docker/nitro/import-wulez-clothes.py: face_U_rise2.
-- Sets already imported keep their ids; these take the next ones in the
-- pack's block. Same rules as 262: 50 credits, faces 0 and owned-only (staff
-- wear every face regardless - FigureDataManager.ProcessFigure). A row whose
-- name or first set some row already has is left alone.

INSERT INTO `catalog_clothing` (`clothing_name`, `clothing_parts`, `price`)
SELECT n.`clothing_name`, n.`clothing_parts`, n.`price`
  FROM (
  SELECT 'face_U_rise2' AS `clothing_name`, '300762' AS `clothing_parts`, 0 AS `price`
  ) n
 WHERE NOT EXISTS (SELECT 1 FROM `catalog_clothing` c
                    WHERE c.`clothing_name` = n.`clothing_name`
                       OR FIND_IN_SET(SUBSTRING_INDEX(n.`clothing_parts`, ',', 1), c.`clothing_parts`) > 0);

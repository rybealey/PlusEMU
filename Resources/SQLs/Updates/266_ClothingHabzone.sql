-- pixelrp: more of the Habzone clothing (see the first ClothingHabzone
-- migration), added by a later run of docker/nitro/import-habzone-clothes.py.
-- Sets already imported keep their ids; these take the next ones in the block.

INSERT INTO `catalog_clothing` (`clothing_name`, `clothing_parts`, `price`)
SELECT n.`clothing_name`, n.`clothing_parts`, n.`price`
  FROM (
  SELECT 'hair_F_leetttbeyh' AS `clothing_name`, '401792' AS `clothing_parts`, 50 AS `price`
  ) n
 WHERE NOT EXISTS (SELECT 1 FROM `catalog_clothing` c
                    WHERE c.`clothing_name` = n.`clothing_name`
                       OR FIND_IN_SET(SUBSTRING_INDEX(n.`clothing_parts`, ',', 1), c.`clothing_parts`) > 0);

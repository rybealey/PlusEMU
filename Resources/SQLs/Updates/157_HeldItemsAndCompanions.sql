-- pixelrp: the held items and the pets go on sale.
--
-- Both used to be undrawable here: this client's renderer did not know the
-- part types Habbo added for them (mc / mcl / mcr, pt / ptl / ptr), and a part
-- whose type the renderer does not know is silently skipped. The client now
-- teaches it those types at startup (ExtendAvatarStructure), so they draw with
-- Habbo's own sprites and sets.
--
-- Held items live in the slot they are worn like, as Habbo's older ones do:
-- the acorn keychain is a belt, the drone a head accessory, and the survival
-- sword, tomahawk, grenade and cyber limbs chest accessories. Their sets moved
-- set type in the figure data this change ships; their set ids did not, so
-- a row here names them the same way as any other piece.
--
-- Pets get a Companions tab in both the store and Choose Your Outfit. Every
-- sellable, selectable pet whose library the hotel has goes on the shelf -
-- the balloon buddy named, the rest on their tidied library name until they
-- are named, like every other backfill.
--
-- 50 credits, as everything else opened at. A set some row already covers is
-- left alone, whatever its price.

-- The seven new pieces, named.
INSERT INTO `catalog_clothing` (`clothing_name`, `clothing_parts`, `display_name`, `price`)
SELECT n.`clothing_name`, n.`clothing_parts`, n.`display_name`, 50
  FROM (
  SELECT 'misc_U_ducketacornkeychain' AS `clothing_name`, '6535' AS `clothing_parts`, 'Acorn Keychain' AS `display_name`
  UNION ALL
  SELECT 'misc_U_drone', '6571', 'Drone'
  UNION ALL
  SELECT 'misc_U_tomahawk', '6579', 'Tomahawk'
  UNION ALL
  SELECT 'misc_U_grenade', '6584', 'Grenade'
  UNION ALL
  SELECT 'misc_U_cyberlimbs', '6600', 'Cyber Limbs'
  UNION ALL
  SELECT 'misc_U_survivalsword', '6606', 'Survival Sword'
  UNION ALL
  SELECT 'pet_U_nftballoonbuddy', '6618', 'Balloon Buddy'
  ) n
 WHERE NOT EXISTS (
       SELECT 1 FROM `catalog_clothing` c
        WHERE FIND_IN_SET(n.`clothing_parts`, REPLACE(c.`clothing_parts`, ' ', '')) > 0);

-- Every other pet.
CREATE TEMPORARY TABLE `rp_pet_backfill` (
  `set_id` int(11) NOT NULL,
  `clothing_name` varchar(55) NOT NULL,
  PRIMARY KEY (`set_id`)
) ENGINE=InnoDB;

INSERT INTO `rp_pet_backfill` (`set_id`, `clothing_name`) VALUES
(5609, 'pet_U_nftshiba1'),
(5610, 'pet_U_nftshiba2'),
(5611, 'pet_U_nftshiba3'),
(5690, 'pet_U_ghostfriend'),
(5742, 'pet_U_nftsummerbird'),
(5826, 'pet_U_goosefriend'),
(5842, 'pet_U_nftbabyrudolf'),
(5843, 'pet_U_nftbabysnowman'),
(5961, 'pet_U_teddycompanion'),
(6031, 'pet_U_nftsnake'),
(6069, 'pet_U_nftcat1'),
(6070, 'pet_U_nftcat2'),
(6077, 'pet_U_bunnycompanion'),
(6119, 'pet_U_unicorncompanion'),
(6150, 'pet_U_nftmonkeyfriend'),
(6166, 'pet_U_shroomie'),
(6178, 'pet_U_cerberuscompanion'),
(6217, 'pet_U_yellowaisha'),
(6266, 'pet_U_minielephant'),
(6299, 'pet_U_mysticdragon'),
(6322, 'pet_U_wisp'),
(6342, 'pet_U_nftshoulderdragon1'),
(6376, 'pet_U_robotcompanion'),
(6381, 'pet_U_shroomietest'),
(6383, 'pet_U_toucan'),
(6442, 'pet_U_nftsquirrel'),
(6487, 'pet_U_nftlittlemonkey'),
(6488, 'pet_U_scallywagseagull'),
(6512, 'pet_U_adorablepetshark'),
(6539, 'pet_U_toastfriend'),
(6553, 'pet_U_nftshoulderdragon2'),
(6555, 'pet_U_raven'),
(6558, 'pet_U_hauntedpuppet'),
(6559, 'pet_U_persiancat'),
(6565, 'pet_U_batfriend'),
(6567, 'pet_U_tinyowl'),
(6577, 'pet_U_possessedpumpkin'),
(6618, 'pet_U_nftballoonbuddy');

INSERT INTO `catalog_clothing` (`clothing_name`, `clothing_parts`, `price`)
SELECT b.`clothing_name`, CAST(b.`set_id` AS CHAR), 50
  FROM `rp_pet_backfill` b
 WHERE NOT EXISTS (
       SELECT 1 FROM `catalog_clothing` c
        WHERE FIND_IN_SET(b.`set_id`, REPLACE(c.`clothing_parts`, ' ', '')) > 0)
 ORDER BY b.`set_id`;

-- Receipts.
SELECT CONCAT('held items on sale: ', COUNT(*)) AS `receipt`
  FROM `catalog_clothing`
 WHERE `clothing_parts` IN ('6535', '6571', '6579', '6584', '6600', '6606') AND `price` > 0;

SELECT CONCAT('pets on sale: ', COUNT(*)) AS `receipt`
  FROM `rp_pet_backfill` b
  JOIN `catalog_clothing` c ON FIND_IN_SET(b.`set_id`, REPLACE(c.`clothing_parts`, ' ', '')) > 0
 WHERE c.`price` > 0;

DROP TEMPORARY TABLE `rp_pet_backfill`;

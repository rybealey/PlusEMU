-- pixelrp: the misc items go on sale, as belts.
--
-- Fifty-five of Habbo's misc pieces - gloves, mittens, scarves, bear paws,
-- plushies, keychains, gift boxes, umbrellas, a boombox, a lasersword - were
-- already in the hotel, sprites and all, and invisible: the renderer did not
-- know their part types until ExtendAvatarStructure, and the editor had
-- nowhere to put them. They now live in Belts, like the acorn keychain before
-- them - the figure data this change ships moves their sets from mc to wa,
-- ids unchanged. One set, a Habbo test item (misc_U_simpleglovetest), stays out.
--
-- A belt is one slot, so one of these at a time, and not with a belt.
--
-- 50 credits like everything else; a set some row already covers keeps that
-- row, whatever its price. Unnamed ones take the tidied library name until
-- they are named.

CREATE TEMPORARY TABLE `rp_belt_backfill` (
  `set_id` int(11) NOT NULL,
  `clothing_name` varchar(55) NOT NULL,
  PRIMARY KEY (`set_id`)
) ENGINE=InnoDB;

INSERT INTO `rp_belt_backfill` (`set_id`, `clothing_name`) VALUES
(3360, 'misc_U_simpleglove'),
(3542, 'misc_U_longscarf'),
(3744, 'misc_U_mittens'),
(3867, 'misc_U_nordicscarf'),
(3975, 'misc_U_silkgloves'),
(4041, 'misc_U_bearpaws'),
(4091, 'misc_U_leathergloves'),
(4137, 'misc_U_muaythai'),
(4282, 'misc_U_train'),
(4356, 'misc_U_bohonecklace'),
(5461, 'misc_U_bunnyplushie'),
(5498, 'misc_U_nftgiftbox'),
(5500, 'misc_U_nftgiftbox2'),
(5518, 'misc_U_python'),
(5569, 'misc_U_sb_raygun'),
(5796, 'misc_U_nftsuitcase'),
(5800, 'misc_U_dreamscarf'),
(5808, 'misc_U_dreamsparkles'),
(5810, 'misc_U_dreamerpillow'),
(5825, 'misc_U_nftspdemon'),
(5829, 'misc_U_nftbiglollipop'),
(5895, 'misc_U_nftsack'),
(5925, 'misc_U_sharkplushie'),
(5942, 'misc_U_umbrella'),
(5996, 'misc_U_nftvalentinesrose'),
(6050, 'misc_U_hearttrumpet'),
(6115, 'misc_U_patchlongscarf'),
(6210, 'misc_U_polkadotmittens'),
(6230, 'misc_U_nftcakeoutfit'),
(6247, 'misc_U_nftumbrella'),
(6251, 'misc_U_nftumbrella2'),
(6255, 'misc_U_nftswordnshield'),
(6346, 'misc_U_kittyplushie'),
(6369, 'misc_U_nftbluerose'),
(6408, 'misc_U_keychainegg'),
(6409, 'misc_U_keychaintoast'),
(6410, 'misc_U_keychainshark'),
(6424, 'misc_U_lasersword'),
(6437, 'misc_U_boombox'),
(6445, 'misc_U_nftledbracelet'),
(6446, 'misc_U_nftledbracelet2'),
(6448, 'misc_U_nftmusicalnotes'),
(6449, 'misc_U_nftyoyo'),
(6451, 'misc_U_nftwarhammer'),
(6452, 'misc_U_goldwatch'),
(6461, 'misc_U_mafiacane'),
(6468, 'misc_U_nftkeytar'),
(6481, 'misc_U_nftrollerskate'),
(6486, 'misc_U_nfttreasurechest'),
(6509, 'misc_F_roxiephone'),
(6527, 'misc_U_nftpinwheel'),
(6547, 'misc_U_lambplush'),
(6548, 'misc_U_glowingfireflies'),
(6552, 'misc_U_ruffledcuffs'),
(6574, 'misc_U_wand');

INSERT INTO `catalog_clothing` (`clothing_name`, `clothing_parts`, `price`)
SELECT b.`clothing_name`, CAST(b.`set_id` AS CHAR), 50
  FROM `rp_belt_backfill` b
 WHERE NOT EXISTS (
       SELECT 1 FROM `catalog_clothing` c
        WHERE FIND_IN_SET(b.`set_id`, REPLACE(c.`clothing_parts`, ' ', '')) > 0)
 ORDER BY b.`set_id`;

-- Receipt: how many of the fifty-five are on the shelf now.
SELECT CONCAT('misc belts on sale: ', COUNT(DISTINCT b.`set_id`)) AS `receipt`
  FROM `rp_belt_backfill` b
  JOIN `catalog_clothing` c ON FIND_IN_SET(b.`set_id`, REPLACE(c.`clothing_parts`, ' ', '')) > 0
 WHERE c.`price` > 0;

DROP TEMPORARY TABLE `rp_belt_backfill`;

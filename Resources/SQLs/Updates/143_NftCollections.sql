-- The NFT year pages, grouped by drop.
--
-- These are not rares and the rule from 141 does not fit them. A rare is
-- named by what it shares at the END - Emerald Hippo, Teal Hippo - because
-- the variant is a colour. An NFT is named by what it shares at the FRONT
-- - Imperial Mode, Vaporware Mode - because the variant is a drop. So the
-- prefix is the key here, longest first, which keeps Blue Origami and
-- Yellow Origami apart instead of collapsing them into Origami.
--
-- 24 drops across the five pages, covering 123 of their 866 lines. The rest
-- are standalone collectibles and stay where they are. Coverage is thin on
-- the newer pages by nature - 2026 has one drop with three or more pieces.
--
-- NFT Mint is not touched here; 142 already gave it the rares treatment,
-- which is right for it because it holds mintable copies of rare furni.

-- ---- NFT: 12 drops ----
SET @pg := (SELECT `id` FROM `catalog_pages` WHERE `caption` = 'NFT' ORDER BY `id` LIMIT 1);
-- 2nd Anniversary (3)
INSERT INTO `catalog_pages` (`id`,`parent_id`,`caption`,`icon_image`,`min_rank`,`min_vip`,`order_num`,`page_link`,`page_layout`,`page_strings_1`,`page_strings_2`,`visible`,`enabled`)
SELECT 949301,@pg,'2nd Anniversary',92,5,0,1,'','default_3x3','','',b'1',b'1' FROM DUAL WHERE @pg IS NOT NULL
ON DUPLICATE KEY UPDATE `parent_id` = @pg, `caption` = '2nd Anniversary', `order_num` = 1;
UPDATE `catalog_items` SET `page_id` = 949301 WHERE `page_id` = @pg AND `item_id` IN ('1000006730','1000006731','1000006732');
-- Blufxus Mode (8)
INSERT INTO `catalog_pages` (`id`,`parent_id`,`caption`,`icon_image`,`min_rank`,`min_vip`,`order_num`,`page_link`,`page_layout`,`page_strings_1`,`page_strings_2`,`visible`,`enabled`)
SELECT 949302,@pg,'Blufxus Mode',92,5,0,2,'','default_3x3','','',b'1',b'1' FROM DUAL WHERE @pg IS NOT NULL
ON DUPLICATE KEY UPDATE `parent_id` = @pg, `caption` = 'Blufxus Mode', `order_num` = 2;
UPDATE `catalog_items` SET `page_id` = 949302 WHERE `page_id` = @pg AND `item_id` IN ('1000006485','1000006490','1000006499','1000006503','1000006511','1000006518','1000006522','1000006528');
-- Cool Cats (5)
INSERT INTO `catalog_pages` (`id`,`parent_id`,`caption`,`icon_image`,`min_rank`,`min_vip`,`order_num`,`page_link`,`page_layout`,`page_strings_1`,`page_strings_2`,`visible`,`enabled`)
SELECT 949303,@pg,'Cool Cats',92,5,0,3,'','default_3x3','','',b'1',b'1' FROM DUAL WHERE @pg IS NOT NULL
ON DUPLICATE KEY UPDATE `parent_id` = @pg, `caption` = 'Cool Cats', `order_num` = 3;
UPDATE `catalog_items` SET `page_id` = 949303 WHERE `page_id` = @pg AND `item_id` IN ('1000005727','1000005733','1000005740','1000011316','1000011317');
-- Ducky Vintaque (4)
INSERT INTO `catalog_pages` (`id`,`parent_id`,`caption`,`icon_image`,`min_rank`,`min_vip`,`order_num`,`page_link`,`page_layout`,`page_strings_1`,`page_strings_2`,`visible`,`enabled`)
SELECT 949304,@pg,'Ducky Vintaque',92,5,0,4,'','default_3x3','','',b'1',b'1' FROM DUAL WHERE @pg IS NOT NULL
ON DUPLICATE KEY UPDATE `parent_id` = @pg, `caption` = 'Ducky Vintaque', `order_num` = 4;
UPDATE `catalog_items` SET `page_id` = 949304 WHERE `page_id` = @pg AND `item_id` IN ('1000006360','1000006363','1000006366','1000006372');
-- Gangnam Duck (4)
INSERT INTO `catalog_pages` (`id`,`parent_id`,`caption`,`icon_image`,`min_rank`,`min_vip`,`order_num`,`page_link`,`page_layout`,`page_strings_1`,`page_strings_2`,`visible`,`enabled`)
SELECT 949305,@pg,'Gangnam Duck',92,5,0,5,'','default_3x3','','',b'1',b'1' FROM DUAL WHERE @pg IS NOT NULL
ON DUPLICATE KEY UPDATE `parent_id` = @pg, `caption` = 'Gangnam Duck', `order_num` = 5;
UPDATE `catalog_items` SET `page_id` = 949305 WHERE `page_id` = @pg AND `item_id` IN ('1000005521','1000005522','1000005523','1000005524');
-- Imperial Mode (9)
INSERT INTO `catalog_pages` (`id`,`parent_id`,`caption`,`icon_image`,`min_rank`,`min_vip`,`order_num`,`page_link`,`page_layout`,`page_strings_1`,`page_strings_2`,`visible`,`enabled`)
SELECT 949306,@pg,'Imperial Mode',92,5,0,6,'','default_3x3','','',b'1',b'1' FROM DUAL WHERE @pg IS NOT NULL
ON DUPLICATE KEY UPDATE `parent_id` = @pg, `caption` = 'Imperial Mode', `order_num` = 6;
UPDATE `catalog_items` SET `page_id` = 949306 WHERE `page_id` = @pg AND `item_id` IN ('1000006475','1000006478','1000006482','1000006489','1000006495','1000006497','1000006508','1000006510','1000006512');
-- Jade Vintaque (4)
INSERT INTO `catalog_pages` (`id`,`parent_id`,`caption`,`icon_image`,`min_rank`,`min_vip`,`order_num`,`page_link`,`page_layout`,`page_strings_1`,`page_strings_2`,`visible`,`enabled`)
SELECT 949307,@pg,'Jade Vintaque',92,5,0,7,'','default_3x3','','',b'1',b'1' FROM DUAL WHERE @pg IS NOT NULL
ON DUPLICATE KEY UPDATE `parent_id` = @pg, `caption` = 'Jade Vintaque', `order_num` = 7;
UPDATE `catalog_items` SET `page_id` = 949307 WHERE `page_id` = @pg AND `item_id` IN ('1000006359','1000006362','1000006367','1000006369');
-- NFT Credit Furni (8)
INSERT INTO `catalog_pages` (`id`,`parent_id`,`caption`,`icon_image`,`min_rank`,`min_vip`,`order_num`,`page_link`,`page_layout`,`page_strings_1`,`page_strings_2`,`visible`,`enabled`)
SELECT 949308,@pg,'NFT Credit Furni',92,5,0,8,'','default_3x3','','',b'1',b'1' FROM DUAL WHERE @pg IS NOT NULL
ON DUPLICATE KEY UPDATE `parent_id` = @pg, `caption` = 'NFT Credit Furni', `order_num` = 8;
UPDATE `catalog_items` SET `page_id` = 949308 WHERE `page_id` = @pg AND `item_id` IN ('1000005279','1000005280','1000005281','1000005282','1000005283','1000006181','1000006182','1000006183');
-- Papa Smurf's (3)
INSERT INTO `catalog_pages` (`id`,`parent_id`,`caption`,`icon_image`,`min_rank`,`min_vip`,`order_num`,`page_link`,`page_layout`,`page_strings_1`,`page_strings_2`,`visible`,`enabled`)
SELECT 949309,@pg,'Papa Smurf''s',92,5,0,9,'','default_3x3','','',b'1',b'1' FROM DUAL WHERE @pg IS NOT NULL
ON DUPLICATE KEY UPDATE `parent_id` = @pg, `caption` = 'Papa Smurf''s', `order_num` = 9;
UPDATE `catalog_items` SET `page_id` = 949309 WHERE `page_id` = @pg AND `item_id` IN ('1000006255','1000006258','1000006259');
-- Pink Blufxus (3)
INSERT INTO `catalog_pages` (`id`,`parent_id`,`caption`,`icon_image`,`min_rank`,`min_vip`,`order_num`,`page_link`,`page_layout`,`page_strings_1`,`page_strings_2`,`visible`,`enabled`)
SELECT 949310,@pg,'Pink Blufxus',92,5,0,10,'','default_3x3','','',b'1',b'1' FROM DUAL WHERE @pg IS NOT NULL
ON DUPLICATE KEY UPDATE `parent_id` = @pg, `caption` = 'Pink Blufxus', `order_num` = 10;
UPDATE `catalog_items` SET `page_id` = 949310 WHERE `page_id` = @pg AND `item_id` IN ('1000006496','1000006501','1000006523');
-- Ruby Vintaque (4)
INSERT INTO `catalog_pages` (`id`,`parent_id`,`caption`,`icon_image`,`min_rank`,`min_vip`,`order_num`,`page_link`,`page_layout`,`page_strings_1`,`page_strings_2`,`visible`,`enabled`)
SELECT 949311,@pg,'Ruby Vintaque',92,5,0,11,'','default_3x3','','',b'1',b'1' FROM DUAL WHERE @pg IS NOT NULL
ON DUPLICATE KEY UPDATE `parent_id` = @pg, `caption` = 'Ruby Vintaque', `order_num` = 11;
UPDATE `catalog_items` SET `page_id` = 949311 WHERE `page_id` = @pg AND `item_id` IN ('1000006358','1000006361','1000006364','1000006370');
-- Vaporware Mode (9)
INSERT INTO `catalog_pages` (`id`,`parent_id`,`caption`,`icon_image`,`min_rank`,`min_vip`,`order_num`,`page_link`,`page_layout`,`page_strings_1`,`page_strings_2`,`visible`,`enabled`)
SELECT 949312,@pg,'Vaporware Mode',92,5,0,12,'','default_3x3','','',b'1',b'1' FROM DUAL WHERE @pg IS NOT NULL
ON DUPLICATE KEY UPDATE `parent_id` = @pg, `caption` = 'Vaporware Mode', `order_num` = 12;
UPDATE `catalog_items` SET `page_id` = 949312 WHERE `page_id` = @pg AND `item_id` IN ('1000006474','1000006484','1000006487','1000006492','1000006493','1000006505','1000006513','1000006514','1000006521');

-- ---- NFT 2023: 5 drops ----
SET @pg := (SELECT `id` FROM `catalog_pages` WHERE `caption` = 'NFT 2023' ORDER BY `id` LIMIT 1);
-- Blue Origami (5)
INSERT INTO `catalog_pages` (`id`,`parent_id`,`caption`,`icon_image`,`min_rank`,`min_vip`,`order_num`,`page_link`,`page_layout`,`page_strings_1`,`page_strings_2`,`visible`,`enabled`)
SELECT 949313,@pg,'Blue Origami',92,5,0,1,'','default_3x3','','',b'1',b'1' FROM DUAL WHERE @pg IS NOT NULL
ON DUPLICATE KEY UPDATE `parent_id` = @pg, `caption` = 'Blue Origami', `order_num` = 1;
UPDATE `catalog_items` SET `page_id` = 949313 WHERE `page_id` = @pg AND `item_id` IN ('1000006708','1000006710','1000006712','1000006717','1000006719');
-- Dark Chocolate (4)
INSERT INTO `catalog_pages` (`id`,`parent_id`,`caption`,`icon_image`,`min_rank`,`min_vip`,`order_num`,`page_link`,`page_layout`,`page_strings_1`,`page_strings_2`,`visible`,`enabled`)
SELECT 949314,@pg,'Dark Chocolate',92,5,0,2,'','default_3x3','','',b'1',b'1' FROM DUAL WHERE @pg IS NOT NULL
ON DUPLICATE KEY UPDATE `parent_id` = @pg, `caption` = 'Dark Chocolate', `order_num` = 2;
UPDATE `catalog_items` SET `page_id` = 949314 WHERE `page_id` = @pg AND `item_id` IN ('1000007059','1000007061','1000007071','1000007080');
-- Indigo X (10)
INSERT INTO `catalog_pages` (`id`,`parent_id`,`caption`,`icon_image`,`min_rank`,`min_vip`,`order_num`,`page_link`,`page_layout`,`page_strings_1`,`page_strings_2`,`visible`,`enabled`)
SELECT 949315,@pg,'Indigo X',92,5,0,3,'','default_3x3','','',b'1',b'1' FROM DUAL WHERE @pg IS NOT NULL
ON DUPLICATE KEY UPDATE `parent_id` = @pg, `caption` = 'Indigo X', `order_num` = 3;
UPDATE `catalog_items` SET `page_id` = 949315 WHERE `page_id` = @pg AND `item_id` IN ('1000006424','1000006427','1000006437','1000006439','1000006452','1000006456','1000006462','1000006463','1000006467','1000006468');
-- White Chocolate (4)
INSERT INTO `catalog_pages` (`id`,`parent_id`,`caption`,`icon_image`,`min_rank`,`min_vip`,`order_num`,`page_link`,`page_layout`,`page_strings_1`,`page_strings_2`,`visible`,`enabled`)
SELECT 949316,@pg,'White Chocolate',92,5,0,4,'','default_3x3','','',b'1',b'1' FROM DUAL WHERE @pg IS NOT NULL
ON DUPLICATE KEY UPDATE `parent_id` = @pg, `caption` = 'White Chocolate', `order_num` = 4;
UPDATE `catalog_items` SET `page_id` = 949316 WHERE `page_id` = @pg AND `item_id` IN ('1000007045','1000007047','1000007056','1000007057');
-- Yellow Origami (5)
INSERT INTO `catalog_pages` (`id`,`parent_id`,`caption`,`icon_image`,`min_rank`,`min_vip`,`order_num`,`page_link`,`page_layout`,`page_strings_1`,`page_strings_2`,`visible`,`enabled`)
SELECT 949317,@pg,'Yellow Origami',92,5,0,5,'','default_3x3','','',b'1',b'1' FROM DUAL WHERE @pg IS NOT NULL
ON DUPLICATE KEY UPDATE `parent_id` = @pg, `caption` = 'Yellow Origami', `order_num` = 5;
UPDATE `catalog_items` SET `page_id` = 949317 WHERE `page_id` = @pg AND `item_id` IN ('1000006703','1000006704','1000006706','1000006716','1000006727');

-- ---- NFT 2024: 3 drops ----
SET @pg := (SELECT `id` FROM `catalog_pages` WHERE `caption` = 'NFT 2024' ORDER BY `id` LIMIT 1);
-- Fluffy Cloud (3)
INSERT INTO `catalog_pages` (`id`,`parent_id`,`caption`,`icon_image`,`min_rank`,`min_vip`,`order_num`,`page_link`,`page_layout`,`page_strings_1`,`page_strings_2`,`visible`,`enabled`)
SELECT 949318,@pg,'Fluffy Cloud',92,5,0,1,'','default_3x3','','',b'1',b'1' FROM DUAL WHERE @pg IS NOT NULL
ON DUPLICATE KEY UPDATE `parent_id` = @pg, `caption` = 'Fluffy Cloud', `order_num` = 1;
UPDATE `catalog_items` SET `page_id` = 949318 WHERE `page_id` = @pg AND `item_id` IN ('1000008381','1000008382','1000008383');
-- Maude's Mini (5)
INSERT INTO `catalog_pages` (`id`,`parent_id`,`caption`,`icon_image`,`min_rank`,`min_vip`,`order_num`,`page_link`,`page_layout`,`page_strings_1`,`page_strings_2`,`visible`,`enabled`)
SELECT 949319,@pg,'Maude''s Mini',92,5,0,2,'','default_3x3','','',b'1',b'1' FROM DUAL WHERE @pg IS NOT NULL
ON DUPLICATE KEY UPDATE `parent_id` = @pg, `caption` = 'Maude''s Mini', `order_num` = 2;
UPDATE `catalog_items` SET `page_id` = 949319 WHERE `page_id` = @pg AND `item_id` IN ('1000007457','1000007458','1000007459','1000007460','1000007462');
-- Miniature Habbo (9)
INSERT INTO `catalog_pages` (`id`,`parent_id`,`caption`,`icon_image`,`min_rank`,`min_vip`,`order_num`,`page_link`,`page_layout`,`page_strings_1`,`page_strings_2`,`visible`,`enabled`)
SELECT 949320,@pg,'Miniature Habbo',92,5,0,3,'','default_3x3','','',b'1',b'1' FROM DUAL WHERE @pg IS NOT NULL
ON DUPLICATE KEY UPDATE `parent_id` = @pg, `caption` = 'Miniature Habbo', `order_num` = 3;
UPDATE `catalog_items` SET `page_id` = 949320 WHERE `page_id` = @pg AND `item_id` IN ('1000007951','1000007953','1000007954','1000007955','1000007956','1000007957','1000007958','1000007959','1000007960');

-- ---- NFT 2025: 3 drops ----
SET @pg := (SELECT `id` FROM `catalog_pages` WHERE `caption` = 'NFT 2025' ORDER BY `id` LIMIT 1);
-- Diamond Paintings (4)
INSERT INTO `catalog_pages` (`id`,`parent_id`,`caption`,`icon_image`,`min_rank`,`min_vip`,`order_num`,`page_link`,`page_layout`,`page_strings_1`,`page_strings_2`,`visible`,`enabled`)
SELECT 949321,@pg,'Diamond Paintings',92,5,0,1,'','default_3x3','','',b'1',b'1' FROM DUAL WHERE @pg IS NOT NULL
ON DUPLICATE KEY UPDATE `parent_id` = @pg, `caption` = 'Diamond Paintings', `order_num` = 1;
UPDATE `catalog_items` SET `page_id` = 949321 WHERE `page_id` = @pg AND `item_id` IN ('1000008809','1000008956','1000009032','1000009560');
-- Ecotron Gen (3)
INSERT INTO `catalog_pages` (`id`,`parent_id`,`caption`,`icon_image`,`min_rank`,`min_vip`,`order_num`,`page_link`,`page_layout`,`page_strings_1`,`page_strings_2`,`visible`,`enabled`)
SELECT 949322,@pg,'Ecotron Gen',92,5,0,2,'','default_3x3','','',b'1',b'1' FROM DUAL WHERE @pg IS NOT NULL
ON DUPLICATE KEY UPDATE `parent_id` = @pg, `caption` = 'Ecotron Gen', `order_num` = 2;
UPDATE `catalog_items` SET `page_id` = 949322 WHERE `page_id` = @pg AND `item_id` IN ('1000008868','1000009034','1000009353');
-- Golden Mini (3)
INSERT INTO `catalog_pages` (`id`,`parent_id`,`caption`,`icon_image`,`min_rank`,`min_vip`,`order_num`,`page_link`,`page_layout`,`page_strings_1`,`page_strings_2`,`visible`,`enabled`)
SELECT 949323,@pg,'Golden Mini',92,5,0,3,'','default_3x3','','',b'1',b'1' FROM DUAL WHERE @pg IS NOT NULL
ON DUPLICATE KEY UPDATE `parent_id` = @pg, `caption` = 'Golden Mini', `order_num` = 3;
UPDATE `catalog_items` SET `page_id` = 949323 WHERE `page_id` = @pg AND `item_id` IN ('1000009198','1000009200','1000009221');

-- ---- NFT 2026: 1 drops ----
SET @pg := (SELECT `id` FROM `catalog_pages` WHERE `caption` = 'NFT 2026' ORDER BY `id` LIMIT 1);
-- Sc 26 (4)
INSERT INTO `catalog_pages` (`id`,`parent_id`,`caption`,`icon_image`,`min_rank`,`min_vip`,`order_num`,`page_link`,`page_layout`,`page_strings_1`,`page_strings_2`,`visible`,`enabled`)
SELECT 949324,@pg,'Sc 26',92,5,0,1,'','default_3x3','','',b'1',b'1' FROM DUAL WHERE @pg IS NOT NULL
ON DUPLICATE KEY UPDATE `parent_id` = @pg, `caption` = 'Sc 26', `order_num` = 1;
UPDATE `catalog_items` SET `page_id` = 949324 WHERE `page_id` = @pg AND `item_id` IN ('1000011128','1000011129','1000011130','1000011131');

-- Any drop left empty because its lines have since moved off the page.
DELETE p FROM `catalog_pages` p
  LEFT JOIN `catalog_items` ci ON ci.`page_id` = p.`id`
 WHERE p.`id` BETWEEN 949301 AND 949324
   AND ci.`page_id` IS NULL;

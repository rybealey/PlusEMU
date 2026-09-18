-- Staff > NFT > NFT, which is still a flat page of 258 lines.
--
-- 143 was meant to have done the drops here and silently did nothing. The
-- hub page and its first child are BOTH captioned 'NFT', and 143 resolved
-- the name to the lower id - the hub - which holds no items at all. So the
-- twelve drops were created under the hub, moved nothing, and were removed
-- again by 143's own empty-category sweep. The four year pages have
-- captions of their own and were unaffected. This file redoes those twelve
-- against the right page, matching on the parent as well as the caption.
--
-- It also adds what 143's rule could never have seen, because that rule
-- only read two or three words from the FRONT:
--   * one-word drops - Trippy (14), Bliss (8)
--   * furni types, which are named at the END - 27 Key Stools, A to Z and
--     Shift, plus Doll (9), Box (8), Furni Crate (6), Poster (6)
--
-- So every 1-3 word prefix AND suffix is scored and the biggest family
-- taken each time, ties to the longer phrase then to a drop over a furni
-- type. Gradings may not head a family - Deluxe, Premium, Basic say how
-- fancy a piece is, not which drop it came from, and left in, 'Deluxe' took
-- three Dolls and two Boxes out of the families they belong to. Same bar
-- 141 puts on colours.
--
-- 31 categories covering 191 of the 258 lines; 67 one-offs stay on the page.

-- The child page, not the hub: same caption, so the parent has to say which.
SET @hub := (SELECT `id` FROM `catalog_pages` WHERE `caption` = 'NFT' ORDER BY `id` LIMIT 1);
SET @pg  := (SELECT `id` FROM `catalog_pages` WHERE `caption` = 'NFT' AND `parent_id` = @hub ORDER BY `id` LIMIT 1);

-- 2nd Anniversary (3, matched at the front)
INSERT INTO `catalog_pages` (`id`,`parent_id`,`caption`,`icon_image`,`min_rank`,`min_vip`,`order_num`,`page_link`,`page_layout`,`page_strings_1`,`page_strings_2`,`visible`,`enabled`)
SELECT 949401,@pg,'2nd Anniversary',92,5,0,1,'','default_3x3','','',b'1',b'1' FROM DUAL WHERE @pg IS NOT NULL
ON DUPLICATE KEY UPDATE `parent_id` = @pg, `caption` = '2nd Anniversary', `order_num` = 1;
UPDATE `catalog_items` SET `page_id` = 949401 WHERE `page_id` = @pg AND `item_id` IN ('1000006730','1000006731','1000006732');

-- Blufxus Mode (8, matched at the front)
INSERT INTO `catalog_pages` (`id`,`parent_id`,`caption`,`icon_image`,`min_rank`,`min_vip`,`order_num`,`page_link`,`page_layout`,`page_strings_1`,`page_strings_2`,`visible`,`enabled`)
SELECT 949402,@pg,'Blufxus Mode',92,5,0,2,'','default_3x3','','',b'1',b'1' FROM DUAL WHERE @pg IS NOT NULL
ON DUPLICATE KEY UPDATE `parent_id` = @pg, `caption` = 'Blufxus Mode', `order_num` = 2;
UPDATE `catalog_items` SET `page_id` = 949402 WHERE `page_id` = @pg AND `item_id` IN ('1000006485','1000006490','1000006499','1000006503','1000006511','1000006518','1000006522','1000006528');

-- Cool Cats (5, matched at the front)
INSERT INTO `catalog_pages` (`id`,`parent_id`,`caption`,`icon_image`,`min_rank`,`min_vip`,`order_num`,`page_link`,`page_layout`,`page_strings_1`,`page_strings_2`,`visible`,`enabled`)
SELECT 949403,@pg,'Cool Cats',92,5,0,3,'','default_3x3','','',b'1',b'1' FROM DUAL WHERE @pg IS NOT NULL
ON DUPLICATE KEY UPDATE `parent_id` = @pg, `caption` = 'Cool Cats', `order_num` = 3;
UPDATE `catalog_items` SET `page_id` = 949403 WHERE `page_id` = @pg AND `item_id` IN ('1000005727','1000005733','1000005740','1000011316','1000011317');

-- Ducky Vintaque (4, matched at the front)
INSERT INTO `catalog_pages` (`id`,`parent_id`,`caption`,`icon_image`,`min_rank`,`min_vip`,`order_num`,`page_link`,`page_layout`,`page_strings_1`,`page_strings_2`,`visible`,`enabled`)
SELECT 949404,@pg,'Ducky Vintaque',92,5,0,4,'','default_3x3','','',b'1',b'1' FROM DUAL WHERE @pg IS NOT NULL
ON DUPLICATE KEY UPDATE `parent_id` = @pg, `caption` = 'Ducky Vintaque', `order_num` = 4;
UPDATE `catalog_items` SET `page_id` = 949404 WHERE `page_id` = @pg AND `item_id` IN ('1000006360','1000006363','1000006366','1000006372');

-- Gangnam Duck (4, matched at the front)
INSERT INTO `catalog_pages` (`id`,`parent_id`,`caption`,`icon_image`,`min_rank`,`min_vip`,`order_num`,`page_link`,`page_layout`,`page_strings_1`,`page_strings_2`,`visible`,`enabled`)
SELECT 949405,@pg,'Gangnam Duck',92,5,0,5,'','default_3x3','','',b'1',b'1' FROM DUAL WHERE @pg IS NOT NULL
ON DUPLICATE KEY UPDATE `parent_id` = @pg, `caption` = 'Gangnam Duck', `order_num` = 5;
UPDATE `catalog_items` SET `page_id` = 949405 WHERE `page_id` = @pg AND `item_id` IN ('1000005521','1000005522','1000005523','1000005524');

-- Imperial Mode (9, matched at the front)
INSERT INTO `catalog_pages` (`id`,`parent_id`,`caption`,`icon_image`,`min_rank`,`min_vip`,`order_num`,`page_link`,`page_layout`,`page_strings_1`,`page_strings_2`,`visible`,`enabled`)
SELECT 949406,@pg,'Imperial Mode',92,5,0,6,'','default_3x3','','',b'1',b'1' FROM DUAL WHERE @pg IS NOT NULL
ON DUPLICATE KEY UPDATE `parent_id` = @pg, `caption` = 'Imperial Mode', `order_num` = 6;
UPDATE `catalog_items` SET `page_id` = 949406 WHERE `page_id` = @pg AND `item_id` IN ('1000006475','1000006478','1000006482','1000006489','1000006495','1000006497','1000006508','1000006510','1000006512');

-- Jade Vintaque (4, matched at the front)
INSERT INTO `catalog_pages` (`id`,`parent_id`,`caption`,`icon_image`,`min_rank`,`min_vip`,`order_num`,`page_link`,`page_layout`,`page_strings_1`,`page_strings_2`,`visible`,`enabled`)
SELECT 949407,@pg,'Jade Vintaque',92,5,0,7,'','default_3x3','','',b'1',b'1' FROM DUAL WHERE @pg IS NOT NULL
ON DUPLICATE KEY UPDATE `parent_id` = @pg, `caption` = 'Jade Vintaque', `order_num` = 7;
UPDATE `catalog_items` SET `page_id` = 949407 WHERE `page_id` = @pg AND `item_id` IN ('1000006359','1000006362','1000006367','1000006369');

-- NFT Credit Furni (8, matched at the front)
INSERT INTO `catalog_pages` (`id`,`parent_id`,`caption`,`icon_image`,`min_rank`,`min_vip`,`order_num`,`page_link`,`page_layout`,`page_strings_1`,`page_strings_2`,`visible`,`enabled`)
SELECT 949408,@pg,'NFT Credit Furni',92,5,0,8,'','default_3x3','','',b'1',b'1' FROM DUAL WHERE @pg IS NOT NULL
ON DUPLICATE KEY UPDATE `parent_id` = @pg, `caption` = 'NFT Credit Furni', `order_num` = 8;
UPDATE `catalog_items` SET `page_id` = 949408 WHERE `page_id` = @pg AND `item_id` IN ('1000005279','1000005280','1000005281','1000005282','1000005283','1000006181','1000006182','1000006183');

-- Papa Smurf's (3, matched at the front)
INSERT INTO `catalog_pages` (`id`,`parent_id`,`caption`,`icon_image`,`min_rank`,`min_vip`,`order_num`,`page_link`,`page_layout`,`page_strings_1`,`page_strings_2`,`visible`,`enabled`)
SELECT 949409,@pg,'Papa Smurf''s',92,5,0,9,'','default_3x3','','',b'1',b'1' FROM DUAL WHERE @pg IS NOT NULL
ON DUPLICATE KEY UPDATE `parent_id` = @pg, `caption` = 'Papa Smurf''s', `order_num` = 9;
UPDATE `catalog_items` SET `page_id` = 949409 WHERE `page_id` = @pg AND `item_id` IN ('1000006255','1000006258','1000006259');

-- Pink Blufxus (3, matched at the front)
INSERT INTO `catalog_pages` (`id`,`parent_id`,`caption`,`icon_image`,`min_rank`,`min_vip`,`order_num`,`page_link`,`page_layout`,`page_strings_1`,`page_strings_2`,`visible`,`enabled`)
SELECT 949410,@pg,'Pink Blufxus',92,5,0,10,'','default_3x3','','',b'1',b'1' FROM DUAL WHERE @pg IS NOT NULL
ON DUPLICATE KEY UPDATE `parent_id` = @pg, `caption` = 'Pink Blufxus', `order_num` = 10;
UPDATE `catalog_items` SET `page_id` = 949410 WHERE `page_id` = @pg AND `item_id` IN ('1000006496','1000006501','1000006523');

-- Ruby Vintaque (4, matched at the front)
INSERT INTO `catalog_pages` (`id`,`parent_id`,`caption`,`icon_image`,`min_rank`,`min_vip`,`order_num`,`page_link`,`page_layout`,`page_strings_1`,`page_strings_2`,`visible`,`enabled`)
SELECT 949411,@pg,'Ruby Vintaque',92,5,0,11,'','default_3x3','','',b'1',b'1' FROM DUAL WHERE @pg IS NOT NULL
ON DUPLICATE KEY UPDATE `parent_id` = @pg, `caption` = 'Ruby Vintaque', `order_num` = 11;
UPDATE `catalog_items` SET `page_id` = 949411 WHERE `page_id` = @pg AND `item_id` IN ('1000006358','1000006361','1000006364','1000006370');

-- Vaporware Mode (9, matched at the front)
INSERT INTO `catalog_pages` (`id`,`parent_id`,`caption`,`icon_image`,`min_rank`,`min_vip`,`order_num`,`page_link`,`page_layout`,`page_strings_1`,`page_strings_2`,`visible`,`enabled`)
SELECT 949412,@pg,'Vaporware Mode',92,5,0,12,'','default_3x3','','',b'1',b'1' FROM DUAL WHERE @pg IS NOT NULL
ON DUPLICATE KEY UPDATE `parent_id` = @pg, `caption` = 'Vaporware Mode', `order_num` = 12;
UPDATE `catalog_items` SET `page_id` = 949412 WHERE `page_id` = @pg AND `item_id` IN ('1000006474','1000006484','1000006487','1000006492','1000006493','1000006505','1000006513','1000006514','1000006521');

-- Bliss (8, matched at the front)
INSERT INTO `catalog_pages` (`id`,`parent_id`,`caption`,`icon_image`,`min_rank`,`min_vip`,`order_num`,`page_link`,`page_layout`,`page_strings_1`,`page_strings_2`,`visible`,`enabled`)
SELECT 949413,@pg,'Bliss',92,5,0,13,'','default_3x3','','',b'1',b'1' FROM DUAL WHERE @pg IS NOT NULL
ON DUPLICATE KEY UPDATE `parent_id` = @pg, `caption` = 'Bliss', `order_num` = 13;
UPDATE `catalog_items` SET `page_id` = 949413 WHERE `page_id` = @pg AND `item_id` IN ('1000006473','1000006480','1000006481','1000006483','1000006502','1000006507','1000006526','1000006527');

-- Box (8, matched at the end)
INSERT INTO `catalog_pages` (`id`,`parent_id`,`caption`,`icon_image`,`min_rank`,`min_vip`,`order_num`,`page_link`,`page_layout`,`page_strings_1`,`page_strings_2`,`visible`,`enabled`)
SELECT 949414,@pg,'Box',92,5,0,14,'','default_3x3','','',b'1',b'1' FROM DUAL WHERE @pg IS NOT NULL
ON DUPLICATE KEY UPDATE `parent_id` = @pg, `caption` = 'Box', `order_num` = 14;
UPDATE `catalog_items` SET `page_id` = 949414 WHERE `page_id` = @pg AND `item_id` IN ('1000005722','1000005744','1000005907','1000005911','1000005914','1000005920','1000006479','1000006805');

-- Cake (5, matched at the end)
INSERT INTO `catalog_pages` (`id`,`parent_id`,`caption`,`icon_image`,`min_rank`,`min_vip`,`order_num`,`page_link`,`page_layout`,`page_strings_1`,`page_strings_2`,`visible`,`enabled`)
SELECT 949415,@pg,'Cake',92,5,0,15,'','default_3x3','','',b'1',b'1' FROM DUAL WHERE @pg IS NOT NULL
ON DUPLICATE KEY UPDATE `parent_id` = @pg, `caption` = 'Cake', `order_num` = 15;
UPDATE `catalog_items` SET `page_id` = 949415 WHERE `page_id` = @pg AND `item_id` IN ('1000005720','1000005723','1000005724','1000005725','1000005735');

-- Car (3, matched at the end)
INSERT INTO `catalog_pages` (`id`,`parent_id`,`caption`,`icon_image`,`min_rank`,`min_vip`,`order_num`,`page_link`,`page_layout`,`page_strings_1`,`page_strings_2`,`visible`,`enabled`)
SELECT 949416,@pg,'Car',92,5,0,16,'','default_3x3','','',b'1',b'1' FROM DUAL WHERE @pg IS NOT NULL
ON DUPLICATE KEY UPDATE `parent_id` = @pg, `caption` = 'Car', `order_num` = 16;
UPDATE `catalog_items` SET `page_id` = 949416 WHERE `page_id` = @pg AND `item_id` IN ('1000005903','1000005921','1000005940');

-- Crafting Powder (4, matched at the end)
INSERT INTO `catalog_pages` (`id`,`parent_id`,`caption`,`icon_image`,`min_rank`,`min_vip`,`order_num`,`page_link`,`page_layout`,`page_strings_1`,`page_strings_2`,`visible`,`enabled`)
SELECT 949417,@pg,'Crafting Powder',92,5,0,17,'','default_3x3','','',b'1',b'1' FROM DUAL WHERE @pg IS NOT NULL
ON DUPLICATE KEY UPDATE `parent_id` = @pg, `caption` = 'Crafting Powder', `order_num` = 17;
UPDATE `catalog_items` SET `page_id` = 949417 WHERE `page_id` = @pg AND `item_id` IN ('1000006476','1000006491','1000006509','1000006515');

-- Doll (9, matched at the end)
INSERT INTO `catalog_pages` (`id`,`parent_id`,`caption`,`icon_image`,`min_rank`,`min_vip`,`order_num`,`page_link`,`page_layout`,`page_strings_1`,`page_strings_2`,`visible`,`enabled`)
SELECT 949418,@pg,'Doll',92,5,0,18,'','default_3x3','','',b'1',b'1' FROM DUAL WHERE @pg IS NOT NULL
ON DUPLICATE KEY UPDATE `parent_id` = @pg, `caption` = 'Doll', `order_num` = 18;
UPDATE `catalog_items` SET `page_id` = 949418 WHERE `page_id` = @pg AND `item_id` IN ('1000005909','1000005918','1000005928','1000005929','1000005930','1000005931','1000005932','1000005935','1000005942');

-- Duck (5, matched at the end)
INSERT INTO `catalog_pages` (`id`,`parent_id`,`caption`,`icon_image`,`min_rank`,`min_vip`,`order_num`,`page_link`,`page_layout`,`page_strings_1`,`page_strings_2`,`visible`,`enabled`)
SELECT 949419,@pg,'Duck',92,5,0,19,'','default_3x3','','',b'1',b'1' FROM DUAL WHERE @pg IS NOT NULL
ON DUPLICATE KEY UPDATE `parent_id` = @pg, `caption` = 'Duck', `order_num` = 19;
UPDATE `catalog_items` SET `page_id` = 949419 WHERE `page_id` = @pg AND `item_id` IN ('1000005656','1000006186','1000006189','1000006340','1000006341');

-- Fridge (5, matched at the end)
INSERT INTO `catalog_pages` (`id`,`parent_id`,`caption`,`icon_image`,`min_rank`,`min_vip`,`order_num`,`page_link`,`page_layout`,`page_strings_1`,`page_strings_2`,`visible`,`enabled`)
SELECT 949420,@pg,'Fridge',92,5,0,20,'','default_3x3','','',b'1',b'1' FROM DUAL WHERE @pg IS NOT NULL
ON DUPLICATE KEY UPDATE `parent_id` = @pg, `caption` = 'Fridge', `order_num` = 20;
UPDATE `catalog_items` SET `page_id` = 949420 WHERE `page_id` = @pg AND `item_id` IN ('1000005440','1000005905','1000005917','1000005923','1000005938');

-- Furni Crate (6, matched at the end)
INSERT INTO `catalog_pages` (`id`,`parent_id`,`caption`,`icon_image`,`min_rank`,`min_vip`,`order_num`,`page_link`,`page_layout`,`page_strings_1`,`page_strings_2`,`visible`,`enabled`)
SELECT 949421,@pg,'Furni Crate',92,5,0,21,'','default_3x3','','',b'1',b'1' FROM DUAL WHERE @pg IS NOT NULL
ON DUPLICATE KEY UPDATE `parent_id` = @pg, `caption` = 'Furni Crate', `order_num` = 21;
UPDATE `catalog_items` SET `page_id` = 949421 WHERE `page_id` = @pg AND `item_id` IN ('1000006488','1000006500','1000006504','1000006506','1000006516','1000006517');

-- Holo Steampunk (4, matched at the end)
INSERT INTO `catalog_pages` (`id`,`parent_id`,`caption`,`icon_image`,`min_rank`,`min_vip`,`order_num`,`page_link`,`page_layout`,`page_strings_1`,`page_strings_2`,`visible`,`enabled`)
SELECT 949422,@pg,'Holo Steampunk',92,5,0,22,'','default_3x3','','',b'1',b'1' FROM DUAL WHERE @pg IS NOT NULL
ON DUPLICATE KEY UPDATE `parent_id` = @pg, `caption` = 'Holo Steampunk', `order_num` = 22;
UPDATE `catalog_items` SET `page_id` = 949422 WHERE `page_id` = @pg AND `item_id` IN ('1000005256','1000005257','1000005258','1000005259');

-- Key Stool (27, matched at the end)
INSERT INTO `catalog_pages` (`id`,`parent_id`,`caption`,`icon_image`,`min_rank`,`min_vip`,`order_num`,`page_link`,`page_layout`,`page_strings_1`,`page_strings_2`,`visible`,`enabled`)
SELECT 949423,@pg,'Key Stool',92,5,0,23,'','default_3x3','','',b'1',b'1' FROM DUAL WHERE @pg IS NOT NULL
ON DUPLICATE KEY UPDATE `parent_id` = @pg, `caption` = 'Key Stool', `order_num` = 23;
UPDATE `catalog_items` SET `page_id` = 949423 WHERE `page_id` = @pg AND `item_id` IN ('1000005444','1000005445','1000005446','1000005447','1000005449','1000005450','1000005453','1000005454','1000005456','1000005457','1000005458','1000005459','1000005460','1000005462','1000005465','1000005466','1000005468','1000005469','1000005470','1000005471','1000005472','1000005473','1000005475','1000005477','1000005478','1000005480','1000005481');

-- Led (3, matched at the end)
INSERT INTO `catalog_pages` (`id`,`parent_id`,`caption`,`icon_image`,`min_rank`,`min_vip`,`order_num`,`page_link`,`page_layout`,`page_strings_1`,`page_strings_2`,`visible`,`enabled`)
SELECT 949424,@pg,'Led',92,5,0,24,'','default_3x3','','',b'1',b'1' FROM DUAL WHERE @pg IS NOT NULL
ON DUPLICATE KEY UPDATE `parent_id` = @pg, `caption` = 'Led', `order_num` = 24;
UPDATE `catalog_items` SET `page_id` = 949424 WHERE `page_id` = @pg AND `item_id` IN ('1000005464','1000005467','1000005476');

-- Metakey Light Decoration (5, matched at the end)
INSERT INTO `catalog_pages` (`id`,`parent_id`,`caption`,`icon_image`,`min_rank`,`min_vip`,`order_num`,`page_link`,`page_layout`,`page_strings_1`,`page_strings_2`,`visible`,`enabled`)
SELECT 949425,@pg,'Metakey Light Decoration',92,5,0,25,'','default_3x3','','',b'1',b'1' FROM DUAL WHERE @pg IS NOT NULL
ON DUPLICATE KEY UPDATE `parent_id` = @pg, `caption` = 'Metakey Light Decoration', `order_num` = 25;
UPDATE `catalog_items` SET `page_id` = 949425 WHERE `page_id` = @pg AND `item_id` IN ('1000011308','1000011309','1000011310','1000011311','1000011312');

-- Poster (6, matched at the end)
INSERT INTO `catalog_pages` (`id`,`parent_id`,`caption`,`icon_image`,`min_rank`,`min_vip`,`order_num`,`page_link`,`page_layout`,`page_strings_1`,`page_strings_2`,`visible`,`enabled`)
SELECT 949426,@pg,'Poster',92,5,0,26,'','default_3x3','','',b'1',b'1' FROM DUAL WHERE @pg IS NOT NULL
ON DUPLICATE KEY UPDATE `parent_id` = @pg, `caption` = 'Poster', `order_num` = 26;
UPDATE `catalog_items` SET `page_id` = 949426 WHERE `page_id` = @pg AND `item_id` IN ('1000011302','1000011303','1000011322','1000011323','1000011324','1000011325');

-- Sofa (4, matched at the end)
INSERT INTO `catalog_pages` (`id`,`parent_id`,`caption`,`icon_image`,`min_rank`,`min_vip`,`order_num`,`page_link`,`page_layout`,`page_strings_1`,`page_strings_2`,`visible`,`enabled`)
SELECT 949427,@pg,'Sofa',92,5,0,27,'','default_3x3','','',b'1',b'1' FROM DUAL WHERE @pg IS NOT NULL
ON DUPLICATE KEY UPDATE `parent_id` = @pg, `caption` = 'Sofa', `order_num` = 27;
UPDATE `catalog_items` SET `page_id` = 949427 WHERE `page_id` = @pg AND `item_id` IN ('1000004699','1000006108','1000006262','1000006380');

-- Table (5, matched at the end)
INSERT INTO `catalog_pages` (`id`,`parent_id`,`caption`,`icon_image`,`min_rank`,`min_vip`,`order_num`,`page_link`,`page_layout`,`page_strings_1`,`page_strings_2`,`visible`,`enabled`)
SELECT 949428,@pg,'Table',92,5,0,28,'','default_3x3','','',b'1',b'1' FROM DUAL WHERE @pg IS NOT NULL
ON DUPLICATE KEY UPDATE `parent_id` = @pg, `caption` = 'Table', `order_num` = 28;
UPDATE `catalog_items` SET `page_id` = 949428 WHERE `page_id` = @pg AND `item_id` IN ('1000005451','1000005463','1000006368','1000006371','1000006498');

-- Trippy (14, matched at the front)
INSERT INTO `catalog_pages` (`id`,`parent_id`,`caption`,`icon_image`,`min_rank`,`min_vip`,`order_num`,`page_link`,`page_layout`,`page_strings_1`,`page_strings_2`,`visible`,`enabled`)
SELECT 949429,@pg,'Trippy',92,5,0,29,'','default_3x3','','',b'1',b'1' FROM DUAL WHERE @pg IS NOT NULL
ON DUPLICATE KEY UPDATE `parent_id` = @pg, `caption` = 'Trippy', `order_num` = 29;
UPDATE `catalog_items` SET `page_id` = 949429 WHERE `page_id` = @pg AND `item_id` IN ('1000005354','1000005379','1000005448','1000005713','1000006026','1000006184','1000006381','1000006519','1000006625','1000006675','1000007082','1000007083','1000009169','1000009230');

-- Trophy (3, matched at the end)
INSERT INTO `catalog_pages` (`id`,`parent_id`,`caption`,`icon_image`,`min_rank`,`min_vip`,`order_num`,`page_link`,`page_layout`,`page_strings_1`,`page_strings_2`,`visible`,`enabled`)
SELECT 949430,@pg,'Trophy',92,5,0,30,'','default_3x3','','',b'1',b'1' FROM DUAL WHERE @pg IS NOT NULL
ON DUPLICATE KEY UPDATE `parent_id` = @pg, `caption` = 'Trophy', `order_num` = 30;
UPDATE `catalog_items` SET `page_id` = 949430 WHERE `page_id` = @pg AND `item_id` IN ('1000005527','1000006187','1000006252');

-- Truck (3, matched at the end)
INSERT INTO `catalog_pages` (`id`,`parent_id`,`caption`,`icon_image`,`min_rank`,`min_vip`,`order_num`,`page_link`,`page_layout`,`page_strings_1`,`page_strings_2`,`visible`,`enabled`)
SELECT 949431,@pg,'Truck',92,5,0,31,'','default_3x3','','',b'1',b'1' FROM DUAL WHERE @pg IS NOT NULL
ON DUPLICATE KEY UPDATE `parent_id` = @pg, `caption` = 'Truck', `order_num` = 31;
UPDATE `catalog_items` SET `page_id` = 949431 WHERE `page_id` = @pg AND `item_id` IN ('1000005904','1000005925','1000005926');

-- Any category left empty because its lines have since moved off the page.
DELETE p FROM `catalog_pages` p
  LEFT JOIN `catalog_items` ci ON ci.`page_id` = p.`id`
 WHERE p.`id` BETWEEN 949401 AND 949431
   AND ci.`page_id` IS NULL;

-- NFT Mint given the same treatment as Rares in 141.
--
-- Of the six pages under Staff > NFT this is the only one the treatment
-- suits, and it suits it because of what it holds: mintable copies of the
-- rare furni, so the families it falls into are the SAME families 141 found
-- next door - Ice Cream Maker, Smoke Machine, Spaceship Door, Dragon Lamp.
-- 236 of its 656 lines are colour variants of 36 furni types and collapse
-- into a category each; the remaining 420 are one-off collectibles and stay
-- on the page, the same way the Monolith stays on Rares.
--
-- The year pages (NFT, NFT 2023..2026) are deliberately untouched: they are
-- themed drops of one-off items, not colour sets, and the same rule groups
-- only 8-44%% of them into folders of two and three - which reads worse than
-- the flat list it replaced.

SET @mint := (SELECT `id` FROM `catalog_pages` WHERE `caption` = 'NFT Mint' ORDER BY `id` LIMIT 1);

-- - aqua (2)
INSERT INTO `catalog_pages` (`id`,`parent_id`,`caption`,`icon_image`,`min_rank`,`min_vip`,`order_num`,`page_link`,`page_layout`,`page_strings_1`,`page_strings_2`,`visible`,`enabled`)
SELECT 949201,@mint,'- aqua',92,5,0,1,'','default_3x3','','',b'1',b'1' FROM DUAL WHERE @mint IS NOT NULL
ON DUPLICATE KEY UPDATE `parent_id` = @mint, `caption` = '- aqua', `order_num` = 1;
UPDATE `catalog_items` SET `page_id` = 949201 WHERE `page_id` = @mint AND `item_id` IN ('1000007368','1000007559');

-- - grass (2)
INSERT INTO `catalog_pages` (`id`,`parent_id`,`caption`,`icon_image`,`min_rank`,`min_vip`,`order_num`,`page_link`,`page_layout`,`page_strings_1`,`page_strings_2`,`visible`,`enabled`)
SELECT 949202,@mint,'- grass',92,5,0,2,'','default_3x3','','',b'1',b'1' FROM DUAL WHERE @mint IS NOT NULL
ON DUPLICATE KEY UPDATE `parent_id` = @mint, `caption` = '- grass', `order_num` = 2;
UPDATE `catalog_items` SET `page_id` = 949202 WHERE `page_id` = @mint AND `item_id` IN ('1000007374','1000007565');

-- - red (2)
INSERT INTO `catalog_pages` (`id`,`parent_id`,`caption`,`icon_image`,`min_rank`,`min_vip`,`order_num`,`page_link`,`page_layout`,`page_strings_1`,`page_strings_2`,`visible`,`enabled`)
SELECT 949203,@mint,'- red',92,5,0,3,'','default_3x3','','',b'1',b'1' FROM DUAL WHERE @mint IS NOT NULL
ON DUPLICATE KEY UPDATE `parent_id` = @mint, `caption` = '- red', `order_num` = 3;
UPDATE `catalog_items` SET `page_id` = 949203 WHERE `page_id` = @mint AND `item_id` IN ('1000007367','1000007558');

-- 1 (3)
INSERT INTO `catalog_pages` (`id`,`parent_id`,`caption`,`icon_image`,`min_rank`,`min_vip`,`order_num`,`page_link`,`page_layout`,`page_strings_1`,`page_strings_2`,`visible`,`enabled`)
SELECT 949204,@mint,'1',92,5,0,4,'','default_3x3','','',b'1',b'1' FROM DUAL WHERE @mint IS NOT NULL
ON DUPLICATE KEY UPDATE `parent_id` = @mint, `caption` = '1', `order_num` = 4;
UPDATE `catalog_items` SET `page_id` = 949204 WHERE `page_id` = @mint AND `item_id` IN ('1000010958','1000010959','1000010960');

-- 2 (4)
INSERT INTO `catalog_pages` (`id`,`parent_id`,`caption`,`icon_image`,`min_rank`,`min_vip`,`order_num`,`page_link`,`page_layout`,`page_strings_1`,`page_strings_2`,`visible`,`enabled`)
SELECT 949205,@mint,'2',92,5,0,5,'','default_3x3','','',b'1',b'1' FROM DUAL WHERE @mint IS NOT NULL
ON DUPLICATE KEY UPDATE `parent_id` = @mint, `caption` = '2', `order_num` = 5;
UPDATE `catalog_items` SET `page_id` = 949205 WHERE `page_id` = @mint AND `item_id` IN ('1000010630','1000010961','1000010963','1000010964');

-- Amber Lamp (7)
INSERT INTO `catalog_pages` (`id`,`parent_id`,`caption`,`icon_image`,`min_rank`,`min_vip`,`order_num`,`page_link`,`page_layout`,`page_strings_1`,`page_strings_2`,`visible`,`enabled`)
SELECT 949206,@mint,'Amber Lamp',92,5,0,6,'','default_3x3','','',b'1',b'1' FROM DUAL WHERE @mint IS NOT NULL
ON DUPLICATE KEY UPDATE `parent_id` = @mint, `caption` = 'Amber Lamp', `order_num` = 6;
UPDATE `catalog_items` SET `page_id` = 949206 WHERE `page_id` = @mint AND `item_id` IN ('1000008432','1000008433','1000008434','1000010197','1000010198','1000010199','1000010200');

-- Book of Knowledge (10)
INSERT INTO `catalog_pages` (`id`,`parent_id`,`caption`,`icon_image`,`min_rank`,`min_vip`,`order_num`,`page_link`,`page_layout`,`page_strings_1`,`page_strings_2`,`visible`,`enabled`)
SELECT 949207,@mint,'Book of Knowledge',92,5,0,7,'','default_3x3','','',b'1',b'1' FROM DUAL WHERE @mint IS NOT NULL
ON DUPLICATE KEY UPDATE `parent_id` = @mint, `caption` = 'Book of Knowledge', `order_num` = 7;
UPDATE `catalog_items` SET `page_id` = 949207 WHERE `page_id` = @mint AND `item_id` IN ('1000010304','1000010305','1000010306','1000010307','1000010308','1000010309','1000010310','1000010311','1000010312','1000010313');

-- Chair (12)
INSERT INTO `catalog_pages` (`id`,`parent_id`,`caption`,`icon_image`,`min_rank`,`min_vip`,`order_num`,`page_link`,`page_layout`,`page_strings_1`,`page_strings_2`,`visible`,`enabled`)
SELECT 949208,@mint,'Chair',92,5,0,8,'','default_3x3','','',b'1',b'1' FROM DUAL WHERE @mint IS NOT NULL
ON DUPLICATE KEY UPDATE `parent_id` = @mint, `caption` = 'Chair', `order_num` = 8;
UPDATE `catalog_items` SET `page_id` = 949208 WHERE `page_id` = @mint AND `item_id` IN ('1000006903','1000006904','1000006910','1000006911','1000006912','1000006913','1000006914','1000006915','1000006916','1000006917','1000006918','1000008437');

-- Doric Pillar (12)
INSERT INTO `catalog_pages` (`id`,`parent_id`,`caption`,`icon_image`,`min_rank`,`min_vip`,`order_num`,`page_link`,`page_layout`,`page_strings_1`,`page_strings_2`,`visible`,`enabled`)
SELECT 949209,@mint,'Doric Pillar',92,5,0,9,'','default_3x3','','',b'1',b'1' FROM DUAL WHERE @mint IS NOT NULL
ON DUPLICATE KEY UPDATE `parent_id` = @mint, `caption` = 'Doric Pillar', `order_num` = 9;
UPDATE `catalog_items` SET `page_id` = 949209 WHERE `page_id` = @mint AND `item_id` IN ('1000007157','1000007158','1000007161','1000007162','1000007163','1000007164','1000007165','1000007166','1000007508','1000007509','1000007510','1000007592');

-- Dragon (2)
INSERT INTO `catalog_pages` (`id`,`parent_id`,`caption`,`icon_image`,`min_rank`,`min_vip`,`order_num`,`page_link`,`page_layout`,`page_strings_1`,`page_strings_2`,`visible`,`enabled`)
SELECT 949210,@mint,'Dragon',92,5,0,10,'','default_3x3','','',b'1',b'1' FROM DUAL WHERE @mint IS NOT NULL
ON DUPLICATE KEY UPDATE `parent_id` = @mint, `caption` = 'Dragon', `order_num` = 10;
UPDATE `catalog_items` SET `page_id` = 949210 WHERE `page_id` = @mint AND `item_id` IN ('1000007467','1000007471');

-- Dragon Lamp (13)
INSERT INTO `catalog_pages` (`id`,`parent_id`,`caption`,`icon_image`,`min_rank`,`min_vip`,`order_num`,`page_link`,`page_layout`,`page_strings_1`,`page_strings_2`,`visible`,`enabled`)
SELECT 949211,@mint,'Dragon Lamp',92,5,0,11,'','default_3x3','','',b'1',b'1' FROM DUAL WHERE @mint IS NOT NULL
ON DUPLICATE KEY UPDATE `parent_id` = @mint, `caption` = 'Dragon Lamp', `order_num` = 11;
UPDATE `catalog_items` SET `page_id` = 949211 WHERE `page_id` = @mint AND `item_id` IN ('1000007035','1000007036','1000007037','1000007038','1000007465','1000007466','1000007468','1000007469','1000007470','1000007472','1000007473','1000007966','1000010954');

-- Elephant (7)
INSERT INTO `catalog_pages` (`id`,`parent_id`,`caption`,`icon_image`,`min_rank`,`min_vip`,`order_num`,`page_link`,`page_layout`,`page_strings_1`,`page_strings_2`,`visible`,`enabled`)
SELECT 949212,@mint,'Elephant',92,5,0,12,'','default_3x3','','',b'1',b'1' FROM DUAL WHERE @mint IS NOT NULL
ON DUPLICATE KEY UPDATE `parent_id` = @mint, `caption` = 'Elephant', `order_num` = 12;
UPDATE `catalog_items` SET `page_id` = 949212 WHERE `page_id` = @mint AND `item_id` IN ('1000007917','1000007944','1000009177','1000009178','1000009179','1000009180','1000010456');

-- Elephant Statue (3)
INSERT INTO `catalog_pages` (`id`,`parent_id`,`caption`,`icon_image`,`min_rank`,`min_vip`,`order_num`,`page_link`,`page_layout`,`page_strings_1`,`page_strings_2`,`visible`,`enabled`)
SELECT 949213,@mint,'Elephant Statue',92,5,0,13,'','default_3x3','','',b'1',b'1' FROM DUAL WHERE @mint IS NOT NULL
ON DUPLICATE KEY UPDATE `parent_id` = @mint, `caption` = 'Elephant Statue', `order_num` = 13;
UPDATE `catalog_items` SET `page_id` = 949213 WHERE `page_id` = @mint AND `item_id` IN ('1000008376','1000010193','1000010457');

-- Fountain (6)
INSERT INTO `catalog_pages` (`id`,`parent_id`,`caption`,`icon_image`,`min_rank`,`min_vip`,`order_num`,`page_link`,`page_layout`,`page_strings_1`,`page_strings_2`,`visible`,`enabled`)
SELECT 949214,@mint,'Fountain',92,5,0,14,'','default_3x3','','',b'1',b'1' FROM DUAL WHERE @mint IS NOT NULL
ON DUPLICATE KEY UPDATE `parent_id` = @mint, `caption` = 'Fountain', `order_num` = 14;
UPDATE `catalog_items` SET `page_id` = 949214 WHERE `page_id` = @mint AND `item_id` IN ('1000007193','1000007194','1000007195','1000007937','1000007939','1000007941');

-- Giant Pillow (7)
INSERT INTO `catalog_pages` (`id`,`parent_id`,`caption`,`icon_image`,`min_rank`,`min_vip`,`order_num`,`page_link`,`page_layout`,`page_strings_1`,`page_strings_2`,`visible`,`enabled`)
SELECT 949215,@mint,'Giant Pillow',92,5,0,15,'','default_3x3','','',b'1',b'1' FROM DUAL WHERE @mint IS NOT NULL
ON DUPLICATE KEY UPDATE `parent_id` = @mint, `caption` = 'Giant Pillow', `order_num` = 15;
UPDATE `catalog_items` SET `page_id` = 949215 WHERE `page_id` = @mint AND `item_id` IN ('1000007365','1000007369','1000007370','1000007371','1000007373','1000007967','1000007968');

-- Gothic fountain (2)
INSERT INTO `catalog_pages` (`id`,`parent_id`,`caption`,`icon_image`,`min_rank`,`min_vip`,`order_num`,`page_link`,`page_layout`,`page_strings_1`,`page_strings_2`,`visible`,`enabled`)
SELECT 949216,@mint,'Gothic fountain',92,5,0,16,'','default_3x3','','',b'1',b'1' FROM DUAL WHERE @mint IS NOT NULL
ON DUPLICATE KEY UPDATE `parent_id` = @mint, `caption` = 'Gothic fountain', `order_num` = 16;
UPDATE `catalog_items` SET `page_id` = 949216 WHERE `page_id` = @mint AND `item_id` IN ('1000011366','1000011367');

-- Holo Tree (2)
INSERT INTO `catalog_pages` (`id`,`parent_id`,`caption`,`icon_image`,`min_rank`,`min_vip`,`order_num`,`page_link`,`page_layout`,`page_strings_1`,`page_strings_2`,`visible`,`enabled`)
SELECT 949217,@mint,'Holo Tree',92,5,0,17,'','default_3x3','','',b'1',b'1' FROM DUAL WHERE @mint IS NOT NULL
ON DUPLICATE KEY UPDATE `parent_id` = @mint, `caption` = 'Holo Tree', `order_num` = 17;
UPDATE `catalog_items` SET `page_id` = 949217 WHERE `page_id` = @mint AND `item_id` IN ('1000010627','1000010628');

-- Ice Cream Maker (14)
INSERT INTO `catalog_pages` (`id`,`parent_id`,`caption`,`icon_image`,`min_rank`,`min_vip`,`order_num`,`page_link`,`page_layout`,`page_strings_1`,`page_strings_2`,`visible`,`enabled`)
SELECT 949218,@mint,'Ice Cream Maker',92,5,0,18,'','default_3x3','','',b'1',b'1' FROM DUAL WHERE @mint IS NOT NULL
ON DUPLICATE KEY UPDATE `parent_id` = @mint, `caption` = 'Ice Cream Maker', `order_num` = 18;
UPDATE `catalog_items` SET `page_id` = 949218 WHERE `page_id` = @mint AND `item_id` IN ('1000007012','1000007013','1000007017','1000007018','1000007019','1000007020','1000007022','1000007023','1000007198','1000007918','1000007919','1000007920','1000007921','1000009435');

-- Laser Portal (7)
INSERT INTO `catalog_pages` (`id`,`parent_id`,`caption`,`icon_image`,`min_rank`,`min_vip`,`order_num`,`page_link`,`page_layout`,`page_strings_1`,`page_strings_2`,`visible`,`enabled`)
SELECT 949219,@mint,'Laser Portal',92,5,0,19,'','default_3x3','','',b'1',b'1' FROM DUAL WHERE @mint IS NOT NULL
ON DUPLICATE KEY UPDATE `parent_id` = @mint, `caption` = 'Laser Portal', `order_num` = 19;
UPDATE `catalog_items` SET `page_id` = 949219 WHERE `page_id` = @mint AND `item_id` IN ('1000007389','1000007570','1000007572','1000007573','1000007574','1000007577','1000007578');

-- Marquee (9)
INSERT INTO `catalog_pages` (`id`,`parent_id`,`caption`,`icon_image`,`min_rank`,`min_vip`,`order_num`,`page_link`,`page_layout`,`page_strings_1`,`page_strings_2`,`visible`,`enabled`)
SELECT 949220,@mint,'Marquee',92,5,0,20,'','default_3x3','','',b'1',b'1' FROM DUAL WHERE @mint IS NOT NULL
ON DUPLICATE KEY UPDATE `parent_id` = @mint, `caption` = 'Marquee', `order_num` = 20;
UPDATE `catalog_items` SET `page_id` = 949220 WHERE `page_id` = @mint AND `item_id` IN ('1000007187','1000007556','1000007557','1000007561','1000007563','1000007564','1000007934','1000007935','1000007936');

-- Oil lamp (2)
INSERT INTO `catalog_pages` (`id`,`parent_id`,`caption`,`icon_image`,`min_rank`,`min_vip`,`order_num`,`page_link`,`page_layout`,`page_strings_1`,`page_strings_2`,`visible`,`enabled`)
SELECT 949221,@mint,'Oil lamp',92,5,0,21,'','default_3x3','','',b'1',b'1' FROM DUAL WHERE @mint IS NOT NULL
ON DUPLICATE KEY UPDATE `parent_id` = @mint, `caption` = 'Oil lamp', `order_num` = 21;
UPDATE `catalog_items` SET `page_id` = 949221 WHERE `page_id` = @mint AND `item_id` IN ('1000006896','1000011343');

-- Oriental Screen (7)
INSERT INTO `catalog_pages` (`id`,`parent_id`,`caption`,`icon_image`,`min_rank`,`min_vip`,`order_num`,`page_link`,`page_layout`,`page_strings_1`,`page_strings_2`,`visible`,`enabled`)
SELECT 949222,@mint,'Oriental Screen',92,5,0,22,'','default_3x3','','',b'1',b'1' FROM DUAL WHERE @mint IS NOT NULL
ON DUPLICATE KEY UPDATE `parent_id` = @mint, `caption` = 'Oriental Screen', `order_num` = 22;
UPDATE `catalog_items` SET `page_id` = 949222 WHERE `page_id` = @mint AND `item_id` IN ('1000007184','1000007185','1000007567','1000007973','1000007975','1000007978','1000007980');

-- Parasol (12)
INSERT INTO `catalog_pages` (`id`,`parent_id`,`caption`,`icon_image`,`min_rank`,`min_vip`,`order_num`,`page_link`,`page_layout`,`page_strings_1`,`page_strings_2`,`visible`,`enabled`)
SELECT 949223,@mint,'Parasol',92,5,0,23,'','default_3x3','','',b'1',b'1' FROM DUAL WHERE @mint IS NOT NULL
ON DUPLICATE KEY UPDATE `parent_id` = @mint, `caption` = 'Parasol', `order_num` = 23;
UPDATE `catalog_items` SET `page_id` = 949223 WHERE `page_id` = @mint AND `item_id` IN ('1000007030','1000007031','1000007032','1000007033','1000007189','1000007190','1000007359','1000007360','1000007361','1000007362','1000007363','1000007463');

-- Pillow (4)
INSERT INTO `catalog_pages` (`id`,`parent_id`,`caption`,`icon_image`,`min_rank`,`min_vip`,`order_num`,`page_link`,`page_layout`,`page_strings_1`,`page_strings_2`,`visible`,`enabled`)
SELECT 949224,@mint,'Pillow',92,5,0,24,'','default_3x3','','',b'1',b'1' FROM DUAL WHERE @mint IS NOT NULL
ON DUPLICATE KEY UPDATE `parent_id` = @mint, `caption` = 'Pillow', `order_num` = 24;
UPDATE `catalog_items` SET `page_id` = 949224 WHERE `page_id` = @mint AND `item_id` IN ('1000007024','1000007366','1000007969','1000008128');

-- Plasto (5)
INSERT INTO `catalog_pages` (`id`,`parent_id`,`caption`,`icon_image`,`min_rank`,`min_vip`,`order_num`,`page_link`,`page_layout`,`page_strings_1`,`page_strings_2`,`visible`,`enabled`)
SELECT 949225,@mint,'Plasto',92,5,0,25,'','default_3x3','','',b'1',b'1' FROM DUAL WHERE @mint IS NOT NULL
ON DUPLICATE KEY UPDATE `parent_id` = @mint, `caption` = 'Plasto', `order_num` = 25;
UPDATE `catalog_items` SET `page_id` = 949225 WHERE `page_id` = @mint AND `item_id` IN ('1000006905','1000006906','1000006907','1000006908','1000006919');

-- Powered Fan (13)
INSERT INTO `catalog_pages` (`id`,`parent_id`,`caption`,`icon_image`,`min_rank`,`min_vip`,`order_num`,`page_link`,`page_layout`,`page_strings_1`,`page_strings_2`,`visible`,`enabled`)
SELECT 949226,@mint,'Powered Fan',92,5,0,26,'','default_3x3','','',b'1',b'1' FROM DUAL WHERE @mint IS NOT NULL
ON DUPLICATE KEY UPDATE `parent_id` = @mint, `caption` = 'Powered Fan', `order_num` = 26;
UPDATE `catalog_items` SET `page_id` = 949226 WHERE `page_id` = @mint AND `item_id` IN ('1000007168','1000007169','1000007170','1000007376','1000007377','1000007378','1000007379','1000007380','1000007382','1000007383','1000007384','1000007386','1000010192');

-- Scifiport (7)
INSERT INTO `catalog_pages` (`id`,`parent_id`,`caption`,`icon_image`,`min_rank`,`min_vip`,`order_num`,`page_link`,`page_layout`,`page_strings_1`,`page_strings_2`,`visible`,`enabled`)
SELECT 949227,@mint,'Scifiport',92,5,0,27,'','default_3x3','','',b'1',b'1' FROM DUAL WHERE @mint IS NOT NULL
ON DUPLICATE KEY UPDATE `parent_id` = @mint, `caption` = 'Scifiport', `order_num` = 27;
UPDATE `catalog_items` SET `page_id` = 949227 WHERE `page_id` = @mint AND `item_id` IN ('1000007388','1000007390','1000007568','1000007569','1000007571','1000007575','1000007576');

-- Sleeping Bag (11)
INSERT INTO `catalog_pages` (`id`,`parent_id`,`caption`,`icon_image`,`min_rank`,`min_vip`,`order_num`,`page_link`,`page_layout`,`page_strings_1`,`page_strings_2`,`visible`,`enabled`)
SELECT 949228,@mint,'Sleeping Bag',92,5,0,28,'','default_3x3','','',b'1',b'1' FROM DUAL WHERE @mint IS NOT NULL
ON DUPLICATE KEY UPDATE `parent_id` = @mint, `caption` = 'Sleeping Bag', `order_num` = 28;
UPDATE `catalog_items` SET `page_id` = 949228 WHERE `page_id` = @mint AND `item_id` IN ('1000007392','1000007393','1000007395','1000007396','1000007398','1000007399','1000007400','1000007401','1000007402','1000007580','1000009035');

-- Sleepingbag (2)
INSERT INTO `catalog_pages` (`id`,`parent_id`,`caption`,`icon_image`,`min_rank`,`min_vip`,`order_num`,`page_link`,`page_layout`,`page_strings_1`,`page_strings_2`,`visible`,`enabled`)
SELECT 949229,@mint,'Sleepingbag',92,5,0,29,'','default_3x3','','',b'1',b'1' FROM DUAL WHERE @mint IS NOT NULL
ON DUPLICATE KEY UPDATE `parent_id` = @mint, `caption` = 'Sleepingbag', `order_num` = 29;
UPDATE `catalog_items` SET `page_id` = 949229 WHERE `page_id` = @mint AND `item_id` IN ('1000007394','1000007579');

-- Smoke Machine (14)
INSERT INTO `catalog_pages` (`id`,`parent_id`,`caption`,`icon_image`,`min_rank`,`min_vip`,`order_num`,`page_link`,`page_layout`,`page_strings_1`,`page_strings_2`,`visible`,`enabled`)
SELECT 949230,@mint,'Smoke Machine',92,5,0,30,'','default_3x3','','',b'1',b'1' FROM DUAL WHERE @mint IS NOT NULL
ON DUPLICATE KEY UPDATE `parent_id` = @mint, `caption` = 'Smoke Machine', `order_num` = 30;
UPDATE `catalog_items` SET `page_id` = 949230 WHERE `page_id` = @mint AND `item_id` IN ('1000007025','1000007026','1000007027','1000007028','1000007403','1000007404','1000007405','1000007406','1000007407','1000007408','1000007411','1000007412','1000007413','1000008966');

-- Spaceship Door (14)
INSERT INTO `catalog_pages` (`id`,`parent_id`,`caption`,`icon_image`,`min_rank`,`min_vip`,`order_num`,`page_link`,`page_layout`,`page_strings_1`,`page_strings_2`,`visible`,`enabled`)
SELECT 949231,@mint,'Spaceship Door',92,5,0,31,'','default_3x3','','',b'1',b'1' FROM DUAL WHERE @mint IS NOT NULL
ON DUPLICATE KEY UPDATE `parent_id` = @mint, `caption` = 'Spaceship Door', `order_num` = 31;
UPDATE `catalog_items` SET `page_id` = 949231 WHERE `page_id` = @mint AND `item_id` IN ('1000007173','1000007174','1000007175','1000007176','1000007177','1000007178','1000007179','1000007181','1000007182','1000007183','1000007924','1000007925','1000007926','1000008130');

-- Square Dining Table (2)
INSERT INTO `catalog_pages` (`id`,`parent_id`,`caption`,`icon_image`,`min_rank`,`min_vip`,`order_num`,`page_link`,`page_layout`,`page_strings_1`,`page_strings_2`,`visible`,`enabled`)
SELECT 949232,@mint,'Square Dining Table',92,5,0,32,'','default_3x3','','',b'1',b'1' FROM DUAL WHERE @mint IS NOT NULL
ON DUPLICATE KEY UPDATE `parent_id` = @mint, `caption` = 'Square Dining Table', `order_num` = 32;
UPDATE `catalog_items` SET `page_id` = 949232 WHERE `page_id` = @mint AND `item_id` IN ('1000006883','1000008373');

-- Table (8)
INSERT INTO `catalog_pages` (`id`,`parent_id`,`caption`,`icon_image`,`min_rank`,`min_vip`,`order_num`,`page_link`,`page_layout`,`page_strings_1`,`page_strings_2`,`visible`,`enabled`)
SELECT 949233,@mint,'Table',92,5,0,33,'','default_3x3','','',b'1',b'1' FROM DUAL WHERE @mint IS NOT NULL
ON DUPLICATE KEY UPDATE `parent_id` = @mint, `caption` = 'Table', `order_num` = 33;
UPDATE `catalog_items` SET `page_id` = 949233 WHERE `page_id` = @mint AND `item_id` IN ('1000006884','1000006886','1000006887','1000006888','1000006891','1000006894','1000006895','1000008371');

-- Throne (4)
INSERT INTO `catalog_pages` (`id`,`parent_id`,`caption`,`icon_image`,`min_rank`,`min_vip`,`order_num`,`page_link`,`page_layout`,`page_strings_1`,`page_strings_2`,`visible`,`enabled`)
SELECT 949234,@mint,'Throne',92,5,0,34,'','default_3x3','','',b'1',b'1' FROM DUAL WHERE @mint IS NOT NULL
ON DUPLICATE KEY UPDATE `parent_id` = @mint, `caption` = 'Throne', `order_num` = 34;
UPDATE `catalog_items` SET `page_id` = 949234 WHERE `page_id` = @mint AND `item_id` IN ('1000007192','1000007197','1000008537','1000009499');

-- Trophy (3)
INSERT INTO `catalog_pages` (`id`,`parent_id`,`caption`,`icon_image`,`min_rank`,`min_vip`,`order_num`,`page_link`,`page_layout`,`page_strings_1`,`page_strings_2`,`visible`,`enabled`)
SELECT 949235,@mint,'Trophy',92,5,0,35,'','default_3x3','','',b'1',b'1' FROM DUAL WHERE @mint IS NOT NULL
ON DUPLICATE KEY UPDATE `parent_id` = @mint, `caption` = 'Trophy', `order_num` = 35;
UPDATE `catalog_items` SET `page_id` = 949235 WHERE `page_id` = @mint AND `item_id` IN ('1000010951','1000010952','1000010953');

-- Walkway (2)
INSERT INTO `catalog_pages` (`id`,`parent_id`,`caption`,`icon_image`,`min_rank`,`min_vip`,`order_num`,`page_link`,`page_layout`,`page_strings_1`,`page_strings_2`,`visible`,`enabled`)
SELECT 949236,@mint,'Walkway',92,5,0,36,'','default_3x3','','',b'1',b'1' FROM DUAL WHERE @mint IS NOT NULL
ON DUPLICATE KEY UPDATE `parent_id` = @mint, `caption` = 'Walkway', `order_num` = 36;
UPDATE `catalog_items` SET `page_id` = 949236 WHERE `page_id` = @mint AND `item_id` IN ('1000010949','1000010950');

-- Any category left empty because its lines have since moved off the page.
DELETE p FROM `catalog_pages` p
  LEFT JOIN `catalog_items` ci ON ci.`page_id` = p.`id`
 WHERE p.`id` BETWEEN 949201 AND 949236
   AND ci.`page_id` IS NULL;

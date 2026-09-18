-- Bonus Rares was one flat page of 330 colour variants - six Lamps, six
-- DuckBooks, thirteen Tortoises - so finding a particular colour meant
-- reading the whole page. This gives every furni type its own
-- subcategory holding all of its variants, and the page itself keeps
-- nothing but those 48 categories.
--
-- Resolved by caption rather than by id: 39_CatalogReorg seeded the page
-- as 920304 but 119_CatalogLibraryRestore rebuilt the library off a
-- runtime @base, so the literal is not safe to assume.

SET @bonus := (SELECT `id` FROM `catalog_pages` WHERE `caption` = 'Bonus Rares' ORDER BY `id` LIMIT 1);

-- Names first, so a renamed row lands in the family it belongs to.
UPDATE `catalog_items` SET `catalog_name` = 'Orange Alien Baby' WHERE `page_id` = @bonus AND `item_id` = '1000010414';
UPDATE `furniture` SET `public_name` = 'Orange Alien Baby' WHERE `item_name` = 'bonusrare26_4*2';
UPDATE `catalog_items` SET `catalog_name` = 'Purple Alien Baby' WHERE `page_id` = @bonus AND `item_id` = '1000010415';
UPDATE `furniture` SET `public_name` = 'Purple Alien Baby' WHERE `item_name` = 'bonusrare26_4*3';
UPDATE `catalog_items` SET `catalog_name` = 'Amber Alien Baby' WHERE `page_id` = @bonus AND `item_id` = '1000010416';
UPDATE `furniture` SET `public_name` = 'Amber Alien Baby' WHERE `item_name` = 'bonusrare26_4*4';
UPDATE `catalog_items` SET `catalog_name` = 'Slate Alien Baby' WHERE `page_id` = @bonus AND `item_id` = '1000010417';
UPDATE `furniture` SET `public_name` = 'Slate Alien Baby' WHERE `item_name` = 'bonusrare26_4*5';
UPDATE `catalog_items` SET `catalog_name` = 'Green Tortoise' WHERE `page_id` = @bonus AND `item_id` = '1000002191';
UPDATE `catalog_items` SET `catalog_name` = 'Yellow Gumball Machine' WHERE `page_id` = @bonus AND `item_id` = '1000004795';

-- Alien Baby (12)
INSERT INTO `catalog_pages` (`id`,`parent_id`,`caption`,`icon_image`,`min_rank`,`min_vip`,`order_num`,`page_link`,`page_layout`,`page_strings_1`,`page_strings_2`,`visible`,`enabled`)
SELECT 949001,@bonus,'Alien Baby',28,5,0,1,'','default_3x3','','',b'1',b'1' FROM DUAL WHERE @bonus IS NOT NULL
ON DUPLICATE KEY UPDATE `parent_id` = @bonus, `caption` = 'Alien Baby', `order_num` = 1;
UPDATE `catalog_items` SET `page_id` = 949001 WHERE `page_id` = @bonus AND `item_id` IN ('1000006016','1000006017','1000006018','1000006019','1000006020','1000006021','1000010412','1000010413','1000010414','1000010415','1000010416','1000010417');

-- Aloe Plant (6)
INSERT INTO `catalog_pages` (`id`,`parent_id`,`caption`,`icon_image`,`min_rank`,`min_vip`,`order_num`,`page_link`,`page_layout`,`page_strings_1`,`page_strings_2`,`visible`,`enabled`)
SELECT 949002,@bonus,'Aloe Plant',28,5,0,2,'','default_3x3','','',b'1',b'1' FROM DUAL WHERE @bonus IS NOT NULL
ON DUPLICATE KEY UPDATE `parent_id` = @bonus, `caption` = 'Aloe Plant', `order_num` = 2;
UPDATE `catalog_items` SET `page_id` = 949002 WHERE `page_id` = @bonus AND `item_id` IN ('1000001541','1000001542','1000001543','1000001544','1000001545','1000001546');

-- Baby Cow (6)
INSERT INTO `catalog_pages` (`id`,`parent_id`,`caption`,`icon_image`,`min_rank`,`min_vip`,`order_num`,`page_link`,`page_layout`,`page_strings_1`,`page_strings_2`,`visible`,`enabled`)
SELECT 949003,@bonus,'Baby Cow',28,5,0,3,'','default_3x3','','',b'1',b'1' FROM DUAL WHERE @bonus IS NOT NULL
ON DUPLICATE KEY UPDATE `parent_id` = @bonus, `caption` = 'Baby Cow', `order_num` = 3;
UPDATE `catalog_items` SET `page_id` = 949003 WHERE `page_id` = @bonus AND `item_id` IN ('1000007310','1000007311','1000007312','1000007313','1000007314','1000007315');

-- Bird of Paradise (4)
INSERT INTO `catalog_pages` (`id`,`parent_id`,`caption`,`icon_image`,`min_rank`,`min_vip`,`order_num`,`page_link`,`page_layout`,`page_strings_1`,`page_strings_2`,`visible`,`enabled`)
SELECT 949004,@bonus,'Bird of Paradise',28,5,0,4,'','default_3x3','','',b'1',b'1' FROM DUAL WHERE @bonus IS NOT NULL
ON DUPLICATE KEY UPDATE `parent_id` = @bonus, `caption` = 'Bird of Paradise', `order_num` = 4;
UPDATE `catalog_items` SET `page_id` = 949004 WHERE `page_id` = @bonus AND `item_id` IN ('8358','8359','8360','8361');

-- Boba (6)
INSERT INTO `catalog_pages` (`id`,`parent_id`,`caption`,`icon_image`,`min_rank`,`min_vip`,`order_num`,`page_link`,`page_layout`,`page_strings_1`,`page_strings_2`,`visible`,`enabled`)
SELECT 949005,@bonus,'Boba',28,5,0,5,'','default_3x3','','',b'1',b'1' FROM DUAL WHERE @bonus IS NOT NULL
ON DUPLICATE KEY UPDATE `parent_id` = @bonus, `caption` = 'Boba', `order_num` = 5;
UPDATE `catalog_items` SET `page_id` = 949005 WHERE `page_id` = @bonus AND `item_id` IN ('1000007323','1000007324','1000007325','1000007326','1000007327','1000007328');

-- Bonsai (6)
INSERT INTO `catalog_pages` (`id`,`parent_id`,`caption`,`icon_image`,`min_rank`,`min_vip`,`order_num`,`page_link`,`page_layout`,`page_strings_1`,`page_strings_2`,`visible`,`enabled`)
SELECT 949006,@bonus,'Bonsai',28,5,0,6,'','default_3x3','','',b'1',b'1' FROM DUAL WHERE @bonus IS NOT NULL
ON DUPLICATE KEY UPDATE `parent_id` = @bonus, `caption` = 'Bonsai', `order_num` = 6;
UPDATE `catalog_items` SET `page_id` = 949006 WHERE `page_id` = @bonus AND `item_id` IN ('1000001152','1000001153','1000001154','1000001155','1000001156','1000001157');

-- Bonus Bags (43)
INSERT INTO `catalog_pages` (`id`,`parent_id`,`caption`,`icon_image`,`min_rank`,`min_vip`,`order_num`,`page_link`,`page_layout`,`page_strings_1`,`page_strings_2`,`visible`,`enabled`)
SELECT 949007,@bonus,'Bonus Bags',28,5,0,7,'','default_3x3','','',b'1',b'1' FROM DUAL WHERE @bonus IS NOT NULL
ON DUPLICATE KEY UPDATE `parent_id` = @bonus, `caption` = 'Bonus Bags', `order_num` = 7;
UPDATE `catalog_items` SET `page_id` = 949007 WHERE `page_id` = @bonus AND `item_id` IN ('1000001252','1000001253','1000001540','1000001720','1000001808','1000002022','1000002023','1000002126','1000002127','1000002134','1000002135','1000002802','1000002924','1000003092','1000003210','1000003276','1000003289','1000003302','1000003303','1000003971','1000003990','1000003997','1000003998','1000004781','1000004789','1000004802','1000004803','1000006109','1000006110','1000006111','1000006112','1000007302','1000007303','1000007322','1000007329','1000008697','1000008704','1000008711','1000008718','1000010390','1000010391','1000010392','1000010393');

-- Cactus Mug (6)
INSERT INTO `catalog_pages` (`id`,`parent_id`,`caption`,`icon_image`,`min_rank`,`min_vip`,`order_num`,`page_link`,`page_layout`,`page_strings_1`,`page_strings_2`,`visible`,`enabled`)
SELECT 949008,@bonus,'Cactus Mug',28,5,0,8,'','default_3x3','','',b'1',b'1' FROM DUAL WHERE @bonus IS NOT NULL
ON DUPLICATE KEY UPDATE `parent_id` = @bonus, `caption` = 'Cactus Mug', `order_num` = 8;
UPDATE `catalog_items` SET `page_id` = 949008 WHERE `page_id` = @bonus AND `item_id` IN ('1000004788','1000005260','1000005261','1000005262','1000005263','1000005264');

-- Crown Cactus (6)
INSERT INTO `catalog_pages` (`id`,`parent_id`,`caption`,`icon_image`,`min_rank`,`min_vip`,`order_num`,`page_link`,`page_layout`,`page_strings_1`,`page_strings_2`,`visible`,`enabled`)
SELECT 949009,@bonus,'Crown Cactus',28,5,0,9,'','default_3x3','','',b'1',b'1' FROM DUAL WHERE @bonus IS NOT NULL
ON DUPLICATE KEY UPDATE `parent_id` = @bonus, `caption` = 'Crown Cactus', `order_num` = 9;
UPDATE `catalog_items` SET `page_id` = 949009 WHERE `page_id` = @bonus AND `item_id` IN ('1000001620','1000001621','1000001622','1000001623','1000001624','1000001625');

-- Daisy (6)
INSERT INTO `catalog_pages` (`id`,`parent_id`,`caption`,`icon_image`,`min_rank`,`min_vip`,`order_num`,`page_link`,`page_layout`,`page_strings_1`,`page_strings_2`,`visible`,`enabled`)
SELECT 949010,@bonus,'Daisy',28,5,0,10,'','default_3x3','','',b'1',b'1' FROM DUAL WHERE @bonus IS NOT NULL
ON DUPLICATE KEY UPDATE `parent_id` = @bonus, `caption` = 'Daisy', `order_num` = 10;
UPDATE `catalog_items` SET `page_id` = 949010 WHERE `page_id` = @bonus AND `item_id` IN ('1000002803','1000002804','1000002805','1000002806','1000002807','1000002808');

-- Desert Rain Frog (6)
INSERT INTO `catalog_pages` (`id`,`parent_id`,`caption`,`icon_image`,`min_rank`,`min_vip`,`order_num`,`page_link`,`page_layout`,`page_strings_1`,`page_strings_2`,`visible`,`enabled`)
SELECT 949011,@bonus,'Desert Rain Frog',28,5,0,11,'','default_3x3','','',b'1',b'1' FROM DUAL WHERE @bonus IS NOT NULL
ON DUPLICATE KEY UPDATE `parent_id` = @bonus, `caption` = 'Desert Rain Frog', `order_num` = 11;
UPDATE `catalog_items` SET `page_id` = 949011 WHERE `page_id` = @bonus AND `item_id` IN ('1000007316','1000007317','1000007318','1000007319','1000007320','1000007321');

-- Dog Plushy (6)
INSERT INTO `catalog_pages` (`id`,`parent_id`,`caption`,`icon_image`,`min_rank`,`min_vip`,`order_num`,`page_link`,`page_layout`,`page_strings_1`,`page_strings_2`,`visible`,`enabled`)
SELECT 949012,@bonus,'Dog Plushy',28,5,0,12,'','default_3x3','','',b'1',b'1' FROM DUAL WHERE @bonus IS NOT NULL
ON DUPLICATE KEY UPDATE `parent_id` = @bonus, `caption` = 'Dog Plushy', `order_num` = 12;
UPDATE `catalog_items` SET `page_id` = 949012 WHERE `page_id` = @bonus AND `item_id` IN ('1000003296','1000003297','1000003298','1000003299','1000003300','1000003301');

-- Doughnut (6)
INSERT INTO `catalog_pages` (`id`,`parent_id`,`caption`,`icon_image`,`min_rank`,`min_vip`,`order_num`,`page_link`,`page_layout`,`page_strings_1`,`page_strings_2`,`visible`,`enabled`)
SELECT 949013,@bonus,'Doughnut',28,5,0,13,'','default_3x3','','',b'1',b'1' FROM DUAL WHERE @bonus IS NOT NULL
ON DUPLICATE KEY UPDATE `parent_id` = @bonus, `caption` = 'Doughnut', `order_num` = 13;
UPDATE `catalog_items` SET `page_id` = 949013 WHERE `page_id` = @bonus AND `item_id` IN ('1000002925','1000002926','1000002927','1000002928','1000002929','1000002930');

-- Drago Plush (6)
INSERT INTO `catalog_pages` (`id`,`parent_id`,`caption`,`icon_image`,`min_rank`,`min_vip`,`order_num`,`page_link`,`page_layout`,`page_strings_1`,`page_strings_2`,`visible`,`enabled`)
SELECT 949014,@bonus,'Drago Plush',28,5,0,14,'','default_3x3','','',b'1',b'1' FROM DUAL WHERE @bonus IS NOT NULL
ON DUPLICATE KEY UPDATE `parent_id` = @bonus, `caption` = 'Drago Plush', `order_num` = 14;
UPDATE `catalog_items` SET `page_id` = 949014 WHERE `page_id` = @bonus AND `item_id` IN ('1000008712','1000008713','1000008714','1000008715','1000008716','1000008717');

-- Duck Rug (6)
INSERT INTO `catalog_pages` (`id`,`parent_id`,`caption`,`icon_image`,`min_rank`,`min_vip`,`order_num`,`page_link`,`page_layout`,`page_strings_1`,`page_strings_2`,`visible`,`enabled`)
SELECT 949015,@bonus,'Duck Rug',28,5,0,15,'','default_3x3','','',b'1',b'1' FROM DUAL WHERE @bonus IS NOT NULL
ON DUPLICATE KEY UPDATE `parent_id` = @bonus, `caption` = 'Duck Rug', `order_num` = 15;
UPDATE `catalog_items` SET `page_id` = 949015 WHERE `page_id` = @bonus AND `item_id` IN ('1000008705','1000008706','1000008707','1000008708','1000008709','1000008710');

-- DuckBook (6)
INSERT INTO `catalog_pages` (`id`,`parent_id`,`caption`,`icon_image`,`min_rank`,`min_vip`,`order_num`,`page_link`,`page_layout`,`page_strings_1`,`page_strings_2`,`visible`,`enabled`)
SELECT 949016,@bonus,'DuckBook',28,5,0,16,'','default_3x3','','',b'1',b'1' FROM DUAL WHERE @bonus IS NOT NULL
ON DUPLICATE KEY UPDATE `parent_id` = @bonus, `caption` = 'DuckBook', `order_num` = 16;
UPDATE `catalog_items` SET `page_id` = 949016 WHERE `page_id` = @bonus AND `item_id` IN ('1000010394','1000010395','1000010396','1000010397','1000010398','1000010399');

-- Easter Island Plant (6)
INSERT INTO `catalog_pages` (`id`,`parent_id`,`caption`,`icon_image`,`min_rank`,`min_vip`,`order_num`,`page_link`,`page_layout`,`page_strings_1`,`page_strings_2`,`visible`,`enabled`)
SELECT 949017,@bonus,'Easter Island Plant',28,5,0,17,'','default_3x3','','',b'1',b'1' FROM DUAL WHERE @bonus IS NOT NULL
ON DUPLICATE KEY UPDATE `parent_id` = @bonus, `caption` = 'Easter Island Plant', `order_num` = 17;
UPDATE `catalog_items` SET `page_id` = 949017 WHERE `page_id` = @bonus AND `item_id` IN ('1000003991','1000003992','1000003993','1000003994','1000003995','1000003996');

-- Espresso Machine (6)
INSERT INTO `catalog_pages` (`id`,`parent_id`,`caption`,`icon_image`,`min_rank`,`min_vip`,`order_num`,`page_link`,`page_layout`,`page_strings_1`,`page_strings_2`,`visible`,`enabled`)
SELECT 949018,@bonus,'Espresso Machine',28,5,0,18,'','default_3x3','','',b'1',b'1' FROM DUAL WHERE @bonus IS NOT NULL
ON DUPLICATE KEY UPDATE `parent_id` = @bonus, `caption` = 'Espresso Machine', `order_num` = 18;
UPDATE `catalog_items` SET `page_id` = 949018 WHERE `page_id` = @bonus AND `item_id` IN ('1000003290','1000003291','1000003292','1000003293','1000003294','1000003295');

-- Gelato (6)
INSERT INTO `catalog_pages` (`id`,`parent_id`,`caption`,`icon_image`,`min_rank`,`min_vip`,`order_num`,`page_link`,`page_layout`,`page_strings_1`,`page_strings_2`,`visible`,`enabled`)
SELECT 949019,@bonus,'Gelato',28,5,0,19,'','default_3x3','','',b'1',b'1' FROM DUAL WHERE @bonus IS NOT NULL
ON DUPLICATE KEY UPDATE `parent_id` = @bonus, `caption` = 'Gelato', `order_num` = 19;
UPDATE `catalog_items` SET `page_id` = 949019 WHERE `page_id` = @bonus AND `item_id` IN ('1000003204','1000003205','1000003206','1000003207','1000003208','1000003209');

-- Gemstone (6)
INSERT INTO `catalog_pages` (`id`,`parent_id`,`caption`,`icon_image`,`min_rank`,`min_vip`,`order_num`,`page_link`,`page_layout`,`page_strings_1`,`page_strings_2`,`visible`,`enabled`)
SELECT 949020,@bonus,'Gemstone',28,5,0,20,'','default_3x3','','',b'1',b'1' FROM DUAL WHERE @bonus IS NOT NULL
ON DUPLICATE KEY UPDATE `parent_id` = @bonus, `caption` = 'Gemstone', `order_num` = 20;
UPDATE `catalog_items` SET `page_id` = 949020 WHERE `page_id` = @bonus AND `item_id` IN ('1000010406','1000010407','1000010408','1000010409','1000010410','1000010411');

-- Gumball Machine (6)
INSERT INTO `catalog_pages` (`id`,`parent_id`,`caption`,`icon_image`,`min_rank`,`min_vip`,`order_num`,`page_link`,`page_layout`,`page_strings_1`,`page_strings_2`,`visible`,`enabled`)
SELECT 949021,@bonus,'Gumball Machine',28,5,0,21,'','default_3x3','','',b'1',b'1' FROM DUAL WHERE @bonus IS NOT NULL
ON DUPLICATE KEY UPDATE `parent_id` = @bonus, `caption` = 'Gumball Machine', `order_num` = 21;
UPDATE `catalog_items` SET `page_id` = 949021 WHERE `page_id` = @bonus AND `item_id` IN ('1000004790','1000004791','1000004792','1000004793','1000004794','1000004795');

-- H Light (6)
INSERT INTO `catalog_pages` (`id`,`parent_id`,`caption`,`icon_image`,`min_rank`,`min_vip`,`order_num`,`page_link`,`page_layout`,`page_strings_1`,`page_strings_2`,`visible`,`enabled`)
SELECT 949022,@bonus,'H Light',28,5,0,22,'','default_3x3','','',b'1',b'1' FROM DUAL WHERE @bonus IS NOT NULL
ON DUPLICATE KEY UPDATE `parent_id` = @bonus, `caption` = 'H Light', `order_num` = 22;
UPDATE `catalog_items` SET `page_id` = 949022 WHERE `page_id` = @bonus AND `item_id` IN ('1000004782','1000004783','1000004784','1000004785','1000004786','1000004787');

-- Hatchling (6)
INSERT INTO `catalog_pages` (`id`,`parent_id`,`caption`,`icon_image`,`min_rank`,`min_vip`,`order_num`,`page_link`,`page_layout`,`page_strings_1`,`page_strings_2`,`visible`,`enabled`)
SELECT 949023,@bonus,'Hatchling',28,5,0,23,'','default_3x3','','',b'1',b'1' FROM DUAL WHERE @bonus IS NOT NULL
ON DUPLICATE KEY UPDATE `parent_id` = @bonus, `caption` = 'Hatchling', `order_num` = 23;
UPDATE `catalog_items` SET `page_id` = 949023 WHERE `page_id` = @bonus AND `item_id` IN ('1000007304','1000007305','1000007306','1000007307','1000007308','1000007309');

-- House Plant (6)
INSERT INTO `catalog_pages` (`id`,`parent_id`,`caption`,`icon_image`,`min_rank`,`min_vip`,`order_num`,`page_link`,`page_layout`,`page_strings_1`,`page_strings_2`,`visible`,`enabled`)
SELECT 949024,@bonus,'House Plant',28,5,0,24,'','default_3x3','','',b'1',b'1' FROM DUAL WHERE @bonus IS NOT NULL
ON DUPLICATE KEY UPDATE `parent_id` = @bonus, `caption` = 'House Plant', `order_num` = 24;
UPDATE `catalog_items` SET `page_id` = 949024 WHERE `page_id` = @bonus AND `item_id` IN ('1000001146','1000001147','1000001148','1000001149','1000001150','1000001151');

-- Juice Dispenser (6)
INSERT INTO `catalog_pages` (`id`,`parent_id`,`caption`,`icon_image`,`min_rank`,`min_vip`,`order_num`,`page_link`,`page_layout`,`page_strings_1`,`page_strings_2`,`visible`,`enabled`)
SELECT 949025,@bonus,'Juice Dispenser',28,5,0,25,'','default_3x3','','',b'1',b'1' FROM DUAL WHERE @bonus IS NOT NULL
ON DUPLICATE KEY UPDATE `parent_id` = @bonus, `caption` = 'Juice Dispenser', `order_num` = 25;
UPDATE `catalog_items` SET `page_id` = 949025 WHERE `page_id` = @bonus AND `item_id` IN ('1000002142','1000002143','1000002144','1000002145','1000002146','1000002147');

-- Kawaii Cat Print (6)
INSERT INTO `catalog_pages` (`id`,`parent_id`,`caption`,`icon_image`,`min_rank`,`min_vip`,`order_num`,`page_link`,`page_layout`,`page_strings_1`,`page_strings_2`,`visible`,`enabled`)
SELECT 949026,@bonus,'Kawaii Cat Print',28,5,0,26,'','default_3x3','','',b'1',b'1' FROM DUAL WHERE @bonus IS NOT NULL
ON DUPLICATE KEY UPDATE `parent_id` = @bonus, `caption` = 'Kawaii Cat Print', `order_num` = 26;
UPDATE `catalog_items` SET `page_id` = 949026 WHERE `page_id` = @bonus AND `item_id` IN ('1000010400','1000010401','1000010402','1000010403','1000010404','1000010405');

-- Lamp (6)
INSERT INTO `catalog_pages` (`id`,`parent_id`,`caption`,`icon_image`,`min_rank`,`min_vip`,`order_num`,`page_link`,`page_layout`,`page_strings_1`,`page_strings_2`,`visible`,`enabled`)
SELECT 949027,@bonus,'Lamp',28,5,0,27,'','default_3x3','','',b'1',b'1' FROM DUAL WHERE @bonus IS NOT NULL
ON DUPLICATE KEY UPDATE `parent_id` = @bonus, `caption` = 'Lamp', `order_num` = 27;
UPDATE `catalog_items` SET `page_id` = 949027 WHERE `page_id` = @bonus AND `item_id` IN ('1000002128','1000002129','1000002130','1000002131','1000002132','1000002133');

-- Lantern (6)
INSERT INTO `catalog_pages` (`id`,`parent_id`,`caption`,`icon_image`,`min_rank`,`min_vip`,`order_num`,`page_link`,`page_layout`,`page_strings_1`,`page_strings_2`,`visible`,`enabled`)
SELECT 949028,@bonus,'Lantern',28,5,0,28,'','default_3x3','','',b'1',b'1' FROM DUAL WHERE @bonus IS NOT NULL
ON DUPLICATE KEY UPDATE `parent_id` = @bonus, `caption` = 'Lantern', `order_num` = 28;
UPDATE `catalog_items` SET `page_id` = 949028 WHERE `page_id` = @bonus AND `item_id` IN ('1000004796','1000004797','1000004798','1000004799','1000004800','1000004801');

-- Leggy Lamp (6)
INSERT INTO `catalog_pages` (`id`,`parent_id`,`caption`,`icon_image`,`min_rank`,`min_vip`,`order_num`,`page_link`,`page_layout`,`page_strings_1`,`page_strings_2`,`visible`,`enabled`)
SELECT 949029,@bonus,'Leggy Lamp',28,5,0,29,'','default_3x3','','',b'1',b'1' FROM DUAL WHERE @bonus IS NOT NULL
ON DUPLICATE KEY UPDATE `parent_id` = @bonus, `caption` = 'Leggy Lamp', `order_num` = 29;
UPDATE `catalog_items` SET `page_id` = 949029 WHERE `page_id` = @bonus AND `item_id` IN ('1000006004','1000006005','1000006006','1000006007','1000006008','1000006009');

-- Lobster Phone (6)
INSERT INTO `catalog_pages` (`id`,`parent_id`,`caption`,`icon_image`,`min_rank`,`min_vip`,`order_num`,`page_link`,`page_layout`,`page_strings_1`,`page_strings_2`,`visible`,`enabled`)
SELECT 949030,@bonus,'Lobster Phone',28,5,0,30,'','default_3x3','','',b'1',b'1' FROM DUAL WHERE @bonus IS NOT NULL
ON DUPLICATE KEY UPDATE `parent_id` = @bonus, `caption` = 'Lobster Phone', `order_num` = 30;
UPDATE `catalog_items` SET `page_id` = 949030 WHERE `page_id` = @bonus AND `item_id` IN ('1000003984','1000003985','1000003986','1000003987','1000003988','1000003989');

-- Long Cat (6)
INSERT INTO `catalog_pages` (`id`,`parent_id`,`caption`,`icon_image`,`min_rank`,`min_vip`,`order_num`,`page_link`,`page_layout`,`page_strings_1`,`page_strings_2`,`visible`,`enabled`)
SELECT 949031,@bonus,'Long Cat',28,5,0,31,'','default_3x3','','',b'1',b'1' FROM DUAL WHERE @bonus IS NOT NULL
ON DUPLICATE KEY UPDATE `parent_id` = @bonus, `caption` = 'Long Cat', `order_num` = 31;
UPDATE `catalog_items` SET `page_id` = 949031 WHERE `page_id` = @bonus AND `item_id` IN ('1000005998','1000005999','1000006000','1000006001','1000006002','1000006003');

-- Mini Vase (6)
INSERT INTO `catalog_pages` (`id`,`parent_id`,`caption`,`icon_image`,`min_rank`,`min_vip`,`order_num`,`page_link`,`page_layout`,`page_strings_1`,`page_strings_2`,`visible`,`enabled`)
SELECT 949032,@bonus,'Mini Vase',28,5,0,32,'','default_3x3','','',b'1',b'1' FROM DUAL WHERE @bonus IS NOT NULL
ON DUPLICATE KEY UPDATE `parent_id` = @bonus, `caption` = 'Mini Vase', `order_num` = 32;
UPDATE `catalog_items` SET `page_id` = 949032 WHERE `page_id` = @bonus AND `item_id` IN ('1000002120','1000002121','1000002122','1000002123','1000002124','1000002125');

-- Money Plant (6)
INSERT INTO `catalog_pages` (`id`,`parent_id`,`caption`,`icon_image`,`min_rank`,`min_vip`,`order_num`,`page_link`,`page_layout`,`page_strings_1`,`page_strings_2`,`visible`,`enabled`)
SELECT 949033,@bonus,'Money Plant',28,5,0,33,'','default_3x3','','',b'1',b'1' FROM DUAL WHERE @bonus IS NOT NULL
ON DUPLICATE KEY UPDATE `parent_id` = @bonus, `caption` = 'Money Plant', `order_num` = 33;
UPDATE `catalog_items` SET `page_id` = 949033 WHERE `page_id` = @bonus AND `item_id` IN ('1000003093','1000003094','1000003095','1000003096','1000003097','1000003098');

-- Paw Rug (6)
INSERT INTO `catalog_pages` (`id`,`parent_id`,`caption`,`icon_image`,`min_rank`,`min_vip`,`order_num`,`page_link`,`page_layout`,`page_strings_1`,`page_strings_2`,`visible`,`enabled`)
SELECT 949034,@bonus,'Paw Rug',28,5,0,34,'','default_3x3','','',b'1',b'1' FROM DUAL WHERE @bonus IS NOT NULL
ON DUPLICATE KEY UPDATE `parent_id` = @bonus, `caption` = 'Paw Rug', `order_num` = 34;
UPDATE `catalog_items` SET `page_id` = 949034 WHERE `page_id` = @bonus AND `item_id` IN ('1000003283','1000003284','1000003285','1000003286','1000003287','1000003288');

-- Penguin Beanbag (6)
INSERT INTO `catalog_pages` (`id`,`parent_id`,`caption`,`icon_image`,`min_rank`,`min_vip`,`order_num`,`page_link`,`page_layout`,`page_strings_1`,`page_strings_2`,`visible`,`enabled`)
SELECT 949035,@bonus,'Penguin Beanbag',28,5,0,35,'','default_3x3','','',b'1',b'1' FROM DUAL WHERE @bonus IS NOT NULL
ON DUPLICATE KEY UPDATE `parent_id` = @bonus, `caption` = 'Penguin Beanbag', `order_num` = 35;
UPDATE `catalog_items` SET `page_id` = 949035 WHERE `page_id` = @bonus AND `item_id` IN ('1000008691','1000008692','1000008693','1000008694','1000008695','1000008696');

-- Potbelly Lamp (6)
INSERT INTO `catalog_pages` (`id`,`parent_id`,`caption`,`icon_image`,`min_rank`,`min_vip`,`order_num`,`page_link`,`page_layout`,`page_strings_1`,`page_strings_2`,`visible`,`enabled`)
SELECT 949036,@bonus,'Potbelly Lamp',28,5,0,36,'','default_3x3','','',b'1',b'1' FROM DUAL WHERE @bonus IS NOT NULL
ON DUPLICATE KEY UPDATE `parent_id` = @bonus, `caption` = 'Potbelly Lamp', `order_num` = 36;
UPDATE `catalog_items` SET `page_id` = 949036 WHERE `page_id` = @bonus AND `item_id` IN ('1000003277','1000003278','1000003279','1000003280','1000003281','1000003282');

-- Rose Bunch (6)
INSERT INTO `catalog_pages` (`id`,`parent_id`,`caption`,`icon_image`,`min_rank`,`min_vip`,`order_num`,`page_link`,`page_layout`,`page_strings_1`,`page_strings_2`,`visible`,`enabled`)
SELECT 949037,@bonus,'Rose Bunch',28,5,0,37,'','default_3x3','','',b'1',b'1' FROM DUAL WHERE @bonus IS NOT NULL
ON DUPLICATE KEY UPDATE `parent_id` = @bonus, `caption` = 'Rose Bunch', `order_num` = 37;
UPDATE `catalog_items` SET `page_id` = 949037 WHERE `page_id` = @bonus AND `item_id` IN ('1000001608','1000001609','1000001610','1000001611','1000001612','1000001613');

-- Rose Pillar (4)
INSERT INTO `catalog_pages` (`id`,`parent_id`,`caption`,`icon_image`,`min_rank`,`min_vip`,`order_num`,`page_link`,`page_layout`,`page_strings_1`,`page_strings_2`,`visible`,`enabled`)
SELECT 949038,@bonus,'Rose Pillar',28,5,0,38,'','default_3x3','','',b'1',b'1' FROM DUAL WHERE @bonus IS NOT NULL
ON DUPLICATE KEY UPDATE `parent_id` = @bonus, `caption` = 'Rose Pillar', `order_num` = 38;
UPDATE `catalog_items` SET `page_id` = 949038 WHERE `page_id` = @bonus AND `item_id` IN ('1000000887','1000000888','1000000889','1000000890');

-- Shrimp Aquarium (6)
INSERT INTO `catalog_pages` (`id`,`parent_id`,`caption`,`icon_image`,`min_rank`,`min_vip`,`order_num`,`page_link`,`page_layout`,`page_strings_1`,`page_strings_2`,`visible`,`enabled`)
SELECT 949039,@bonus,'Shrimp Aquarium',28,5,0,39,'','default_3x3','','',b'1',b'1' FROM DUAL WHERE @bonus IS NOT NULL
ON DUPLICATE KEY UPDATE `parent_id` = @bonus, `caption` = 'Shrimp Aquarium', `order_num` = 39;
UPDATE `catalog_items` SET `page_id` = 949039 WHERE `page_id` = @bonus AND `item_id` IN ('1000006010','1000006011','1000006012','1000006013','1000006014','1000006015');

-- Sleepy Planter (6)
INSERT INTO `catalog_pages` (`id`,`parent_id`,`caption`,`icon_image`,`min_rank`,`min_vip`,`order_num`,`page_link`,`page_layout`,`page_strings_1`,`page_strings_2`,`visible`,`enabled`)
SELECT 949040,@bonus,'Sleepy Planter',28,5,0,40,'','default_3x3','','',b'1',b'1' FROM DUAL WHERE @bonus IS NOT NULL
ON DUPLICATE KEY UPDATE `parent_id` = @bonus, `caption` = 'Sleepy Planter', `order_num` = 40;
UPDATE `catalog_items` SET `page_id` = 949040 WHERE `page_id` = @bonus AND `item_id` IN ('1000008698','1000008699','1000008700','1000008701','1000008702','1000008703');

-- Spotted Teapot (6)
INSERT INTO `catalog_pages` (`id`,`parent_id`,`caption`,`icon_image`,`min_rank`,`min_vip`,`order_num`,`page_link`,`page_layout`,`page_strings_1`,`page_strings_2`,`visible`,`enabled`)
SELECT 949041,@bonus,'Spotted Teapot',28,5,0,41,'','default_3x3','','',b'1',b'1' FROM DUAL WHERE @bonus IS NOT NULL
ON DUPLICATE KEY UPDATE `parent_id` = @bonus, `caption` = 'Spotted Teapot', `order_num` = 41;
UPDATE `catalog_items` SET `page_id` = 949041 WHERE `page_id` = @bonus AND `item_id` IN ('1000003972','1000003973','1000003974','1000003975','1000003976','1000003977');

-- Succulent Plant (6)
INSERT INTO `catalog_pages` (`id`,`parent_id`,`caption`,`icon_image`,`min_rank`,`min_vip`,`order_num`,`page_link`,`page_layout`,`page_strings_1`,`page_strings_2`,`visible`,`enabled`)
SELECT 949042,@bonus,'Succulent Plant',28,5,0,42,'','default_3x3','','',b'1',b'1' FROM DUAL WHERE @bonus IS NOT NULL
ON DUPLICATE KEY UPDATE `parent_id` = @bonus, `caption` = 'Succulent Plant', `order_num` = 42;
UPDATE `catalog_items` SET `page_id` = 949042 WHERE `page_id` = @bonus AND `item_id` IN ('1000002136','1000002137','1000002138','1000002139','1000002140','1000002141');

-- Tortoise (13)
INSERT INTO `catalog_pages` (`id`,`parent_id`,`caption`,`icon_image`,`min_rank`,`min_vip`,`order_num`,`page_link`,`page_layout`,`page_strings_1`,`page_strings_2`,`visible`,`enabled`)
SELECT 949043,@bonus,'Tortoise',28,5,0,43,'','default_3x3','','',b'1',b'1' FROM DUAL WHERE @bonus IS NOT NULL
ON DUPLICATE KEY UPDATE `parent_id` = @bonus, `caption` = 'Tortoise', `order_num` = 43;
UPDATE `catalog_items` SET `page_id` = 949043 WHERE `page_id` = @bonus AND `item_id` IN ('1000002186','1000002187','1000002188','1000002189','1000002190','1000002191','1000002192','1000002193','1000002194','1000002195','1000002196','1000002197','1000002198');

-- Tulips (6)
INSERT INTO `catalog_pages` (`id`,`parent_id`,`caption`,`icon_image`,`min_rank`,`min_vip`,`order_num`,`page_link`,`page_layout`,`page_strings_1`,`page_strings_2`,`visible`,`enabled`)
SELECT 949044,@bonus,'Tulips',28,5,0,44,'','default_3x3','','',b'1',b'1' FROM DUAL WHERE @bonus IS NOT NULL
ON DUPLICATE KEY UPDATE `parent_id` = @bonus, `caption` = 'Tulips', `order_num` = 44;
UPDATE `catalog_items` SET `page_id` = 949044 WHERE `page_id` = @bonus AND `item_id` IN ('1000001602','1000001603','1000001604','1000001605','1000001606','1000001607');

-- Vase (4)
INSERT INTO `catalog_pages` (`id`,`parent_id`,`caption`,`icon_image`,`min_rank`,`min_vip`,`order_num`,`page_link`,`page_layout`,`page_strings_1`,`page_strings_2`,`visible`,`enabled`)
SELECT 949045,@bonus,'Vase',28,5,0,45,'','default_3x3','','',b'1',b'1' FROM DUAL WHERE @bonus IS NOT NULL
ON DUPLICATE KEY UPDATE `parent_id` = @bonus, `caption` = 'Vase', `order_num` = 45;
UPDATE `catalog_items` SET `page_id` = 949045 WHERE `page_id` = @bonus AND `item_id` IN ('8354','8355','8356','8357');

-- Vase Stand (6)
INSERT INTO `catalog_pages` (`id`,`parent_id`,`caption`,`icon_image`,`min_rank`,`min_vip`,`order_num`,`page_link`,`page_layout`,`page_strings_1`,`page_strings_2`,`visible`,`enabled`)
SELECT 949046,@bonus,'Vase Stand',28,5,0,46,'','default_3x3','','',b'1',b'1' FROM DUAL WHERE @bonus IS NOT NULL
ON DUPLICATE KEY UPDATE `parent_id` = @bonus, `caption` = 'Vase Stand', `order_num` = 46;
UPDATE `catalog_items` SET `page_id` = 949046 WHERE `page_id` = @bonus AND `item_id` IN ('1000001614','1000001615','1000001616','1000001617','1000001618','1000001619');

-- Water Lily Pot (4)
INSERT INTO `catalog_pages` (`id`,`parent_id`,`caption`,`icon_image`,`min_rank`,`min_vip`,`order_num`,`page_link`,`page_layout`,`page_strings_1`,`page_strings_2`,`visible`,`enabled`)
SELECT 949047,@bonus,'Water Lily Pot',28,5,0,47,'','default_3x3','','',b'1',b'1' FROM DUAL WHERE @bonus IS NOT NULL
ON DUPLICATE KEY UPDATE `parent_id` = @bonus, `caption` = 'Water Lily Pot', `order_num` = 47;
UPDATE `catalog_items` SET `page_id` = 949047 WHERE `page_id` = @bonus AND `item_id` IN ('8350','8351','8352','8353');

-- Welly Planter (6)
INSERT INTO `catalog_pages` (`id`,`parent_id`,`caption`,`icon_image`,`min_rank`,`min_vip`,`order_num`,`page_link`,`page_layout`,`page_strings_1`,`page_strings_2`,`visible`,`enabled`)
SELECT 949048,@bonus,'Welly Planter',28,5,0,48,'','default_3x3','','',b'1',b'1' FROM DUAL WHERE @bonus IS NOT NULL
ON DUPLICATE KEY UPDATE `parent_id` = @bonus, `caption` = 'Welly Planter', `order_num` = 48;
UPDATE `catalog_items` SET `page_id` = 949048 WHERE `page_id` = @bonus AND `item_id` IN ('1000003978','1000003979','1000003980','1000003981','1000003982','1000003983');

-- The item list above was read off 39_CatalogReorg, and a few rows have left
-- the page since - 134 moved the juice dispensers to Builders > Corporations
-- > Cafe. Their category would otherwise sit there empty, so any category
-- that ended up with nothing in it is dropped again.
DELETE p FROM `catalog_pages` p
  LEFT JOIN `catalog_items` ci ON ci.`page_id` = p.`id`
 WHERE p.`id` BETWEEN 949001 AND 949048
   AND ci.`page_id` IS NULL;

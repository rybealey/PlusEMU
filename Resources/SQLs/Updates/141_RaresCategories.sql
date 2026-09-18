categories: 38   grouped: 399   left on page: 127
after absorb -> categories: 38   grouped: 441   left on page: 85
-- The same treatment 140 gave Bonus Rares, now for its sibling page.
-- Rares is a harder shape: 526 lines, of which 441 are colour or one-off
-- variants of 38 furni types and 85 are genuine one-offs - the Monolith,
-- Venus de Habbo, the Duckmaster. The variants get a category each; the
-- one-offs stay on the page, because that is what they are.
--
-- Grouping was read off the page rather than guessed. A leading word counts
-- as a variant word when the same remainder also appears behind a different
-- leading word ('Emerald Hippo' next to 'Teal Hippo' makes both colours), so
-- the gemstone names this page uses - Citrine, Tanzanite, Rhodochrosite -
-- were learned rather than listed. Stripping repeats, which is what lets
-- 'Rose Gold' and 'Ultra Light Blue' come off without being spelled out.
-- Then anything still alone that CONTAINS a family's name joins it, longest
-- family first - so an Elephant Statue is not filed under Elephant, and
-- 'Fire Dragon Lamp' sits with the other Dragon Lamps.

SET @rares := (SELECT p.`id` FROM `catalog_pages` p
               JOIN `catalog_pages` s ON s.`id` = p.`parent_id`
              WHERE p.`caption` = 'Rares' AND s.`caption` = 'Rares' ORDER BY p.`id` LIMIT 1);

-- Amber Lamp (8)
INSERT INTO `catalog_pages` (`id`,`parent_id`,`caption`,`icon_image`,`min_rank`,`min_vip`,`order_num`,`page_link`,`page_layout`,`page_strings_1`,`page_strings_2`,`visible`,`enabled`)
SELECT 949101,@rares,'Amber Lamp',28,5,0,1,'','default_3x3','','',b'1',b'1' FROM DUAL WHERE @rares IS NOT NULL
ON DUPLICATE KEY UPDATE `parent_id` = @rares, `caption` = 'Amber Lamp', `order_num` = 1;
UPDATE `catalog_items` SET `page_id` = 949101 WHERE `page_id` = @rares AND `item_id` IN ('1000001398','1000001923','1000002474','1000003576','1000006057','261','262','8217');

-- Anteater (15)
INSERT INTO `catalog_pages` (`id`,`parent_id`,`caption`,`icon_image`,`min_rank`,`min_vip`,`order_num`,`page_link`,`page_layout`,`page_strings_1`,`page_strings_2`,`visible`,`enabled`)
SELECT 949102,@rares,'Anteater',28,5,0,2,'','default_3x3','','',b'1',b'1' FROM DUAL WHERE @rares IS NOT NULL
ON DUPLICATE KEY UPDATE `parent_id` = @rares, `caption` = 'Anteater', `order_num` = 2;
UPDATE `catalog_items` SET `page_id` = 949102 WHERE `page_id` = @rares AND `item_id` IN ('1000004765','1000004766','1000004767','1000004768','1000004769','1000004770','1000004771','1000004772','1000004773','1000004774','1000004775','1000004776','1000004777','1000004778','1000005899');

-- Baby Penguin (15)
INSERT INTO `catalog_pages` (`id`,`parent_id`,`caption`,`icon_image`,`min_rank`,`min_vip`,`order_num`,`page_link`,`page_layout`,`page_strings_1`,`page_strings_2`,`visible`,`enabled`)
SELECT 949103,@rares,'Baby Penguin',28,5,0,3,'','default_3x3','','',b'1',b'1' FROM DUAL WHERE @rares IS NOT NULL
ON DUPLICATE KEY UPDATE `parent_id` = @rares, `caption` = 'Baby Penguin', `order_num` = 3;
UPDATE `catalog_items` SET `page_id` = 949103 WHERE `page_id` = @rares AND `item_id` IN ('1000005946','1000005947','1000005948','1000005949','1000005950','1000005951','1000005952','1000005953','1000005954','1000005955','1000005956','1000005957','1000005958','1000005959','1000007008');

-- Baby Sheep (15)
INSERT INTO `catalog_pages` (`id`,`parent_id`,`caption`,`icon_image`,`min_rank`,`min_vip`,`order_num`,`page_link`,`page_layout`,`page_strings_1`,`page_strings_2`,`visible`,`enabled`)
SELECT 949104,@rares,'Baby Sheep',28,5,0,4,'','default_3x3','','',b'1',b'1' FROM DUAL WHERE @rares IS NOT NULL
ON DUPLICATE KEY UPDATE `parent_id` = @rares, `caption` = 'Baby Sheep', `order_num` = 4;
UPDATE `catalog_items` SET `page_id` = 949104 WHERE `page_id` = @rares AND `item_id` IN ('1000008633','1000008634','1000008635','1000008636','1000008637','1000008638','1000008639','1000008640','1000008641','1000008642','1000008643','1000008644','1000008645','1000008646','1000008647');

-- Book of Knowledge (10)
INSERT INTO `catalog_pages` (`id`,`parent_id`,`caption`,`icon_image`,`min_rank`,`min_vip`,`order_num`,`page_link`,`page_layout`,`page_strings_1`,`page_strings_2`,`visible`,`enabled`)
SELECT 949105,@rares,'Book of Knowledge',28,5,0,5,'','default_3x3','','',b'1',b'1' FROM DUAL WHERE @rares IS NOT NULL
ON DUPLICATE KEY UPDATE `parent_id` = @rares, `caption` = 'Book of Knowledge', `order_num` = 5;
UPDATE `catalog_items` SET `page_id` = 949105 WHERE `page_id` = @rares AND `item_id` IN ('5682','5683','5684','5685','5686','5687','5688','5689','5690','5691');

-- Capybara (15)
INSERT INTO `catalog_pages` (`id`,`parent_id`,`caption`,`icon_image`,`min_rank`,`min_vip`,`order_num`,`page_link`,`page_layout`,`page_strings_1`,`page_strings_2`,`visible`,`enabled`)
SELECT 949106,@rares,'Capybara',28,5,0,6,'','default_3x3','','',b'1',b'1' FROM DUAL WHERE @rares IS NOT NULL
ON DUPLICATE KEY UPDATE `parent_id` = @rares, `caption` = 'Capybara', `order_num` = 6;
UPDATE `catalog_items` SET `page_id` = 949106 WHERE `page_id` = @rares AND `item_id` IN ('1000004028','1000004029','1000004030','1000004031','1000004032','1000004033','1000004034','1000004035','1000004036','1000004037','1000004038','1000004039','1000004040','1000004041','1000004682');

-- Chest of Light (3)
INSERT INTO `catalog_pages` (`id`,`parent_id`,`caption`,`icon_image`,`min_rank`,`min_vip`,`order_num`,`page_link`,`page_layout`,`page_strings_1`,`page_strings_2`,`visible`,`enabled`)
SELECT 949107,@rares,'Chest of Light',28,5,0,7,'','default_3x3','','',b'1',b'1' FROM DUAL WHERE @rares IS NOT NULL
ON DUPLICATE KEY UPDATE `parent_id` = @rares, `caption` = 'Chest of Light', `order_num` = 7;
UPDATE `catalog_items` SET `page_id` = 949107 WHERE `page_id` = @rares AND `item_id` IN ('1000001393','1000001926','1000002471');

-- Colourable Shishilamp (15)
INSERT INTO `catalog_pages` (`id`,`parent_id`,`caption`,`icon_image`,`min_rank`,`min_vip`,`order_num`,`page_link`,`page_layout`,`page_strings_1`,`page_strings_2`,`visible`,`enabled`)
SELECT 949108,@rares,'Colourable Shishilamp',28,5,0,8,'','default_3x3','','',b'1',b'1' FROM DUAL WHERE @rares IS NOT NULL
ON DUPLICATE KEY UPDATE `parent_id` = @rares, `caption` = 'Colourable Shishilamp', `order_num` = 8;
UPDATE `catalog_items` SET `page_id` = 949108 WHERE `page_id` = @rares AND `item_id` IN ('1000009691','1000009692','1000009693','1000009694','1000009695','1000009696','1000009697','1000009698','1000009700','1000009701','1000009702','1000009703','1000009704','1000009705','1000009706');

-- DJ Turntable (2)
INSERT INTO `catalog_pages` (`id`,`parent_id`,`caption`,`icon_image`,`min_rank`,`min_vip`,`order_num`,`page_link`,`page_layout`,`page_strings_1`,`page_strings_2`,`visible`,`enabled`)
SELECT 949109,@rares,'DJ Turntable',28,5,0,9,'','default_3x3','','',b'1',b'1' FROM DUAL WHERE @rares IS NOT NULL
ON DUPLICATE KEY UPDATE `parent_id` = @rares, `caption` = 'DJ Turntable', `order_num` = 9;
UPDATE `catalog_items` SET `page_id` = 949109 WHERE `page_id` = @rares AND `item_id` IN ('1000006373','273');

-- Doric Pillar (12)
INSERT INTO `catalog_pages` (`id`,`parent_id`,`caption`,`icon_image`,`min_rank`,`min_vip`,`order_num`,`page_link`,`page_layout`,`page_strings_1`,`page_strings_2`,`visible`,`enabled`)
SELECT 949110,@rares,'Doric Pillar',28,5,0,10,'','default_3x3','','',b'1',b'1' FROM DUAL WHERE @rares IS NOT NULL
ON DUPLICATE KEY UPDATE `parent_id` = @rares, `caption` = 'Doric Pillar', `order_num` = 10;
UPDATE `catalog_items` SET `page_id` = 949110 WHERE `page_id` = @rares AND `item_id` IN ('1000001397','1000001922','1000002473','1000003578','20025','398','400','401','403','404','405','406');

-- Dragon (6)
INSERT INTO `catalog_pages` (`id`,`parent_id`,`caption`,`icon_image`,`min_rank`,`min_vip`,`order_num`,`page_link`,`page_layout`,`page_strings_1`,`page_strings_2`,`visible`,`enabled`)
SELECT 949111,@rares,'Dragon',28,5,0,11,'','default_3x3','','',b'1',b'1' FROM DUAL WHERE @rares IS NOT NULL
ON DUPLICATE KEY UPDATE `parent_id` = @rares, `caption` = 'Dragon', `order_num` = 11;
UPDATE `catalog_items` SET `page_id` = 949111 WHERE `page_id` = @rares AND `item_id` IN ('1000000588','30723','410','412','414','4833');

-- Dragon Egg (3)
INSERT INTO `catalog_pages` (`id`,`parent_id`,`caption`,`icon_image`,`min_rank`,`min_vip`,`order_num`,`page_link`,`page_layout`,`page_strings_1`,`page_strings_2`,`visible`,`enabled`)
SELECT 949112,@rares,'Dragon Egg',28,5,0,12,'','default_3x3','','',b'1',b'1' FROM DUAL WHERE @rares IS NOT NULL
ON DUPLICATE KEY UPDATE `parent_id` = @rares, `caption` = 'Dragon Egg', `order_num` = 12;
UPDATE `catalog_items` SET `page_id` = 949112 WHERE `page_id` = @rares AND `item_id` IN ('1000000590','227','4701');

-- Dragon Lamp (20)
INSERT INTO `catalog_pages` (`id`,`parent_id`,`caption`,`icon_image`,`min_rank`,`min_vip`,`order_num`,`page_link`,`page_layout`,`page_strings_1`,`page_strings_2`,`visible`,`enabled`)
SELECT 949113,@rares,'Dragon Lamp',28,5,0,13,'','default_3x3','','',b'1',b'1' FROM DUAL WHERE @rares IS NOT NULL
ON DUPLICATE KEY UPDATE `parent_id` = @rares, `caption` = 'Dragon Lamp', `order_num` = 13;
UPDATE `catalog_items` SET `page_id` = 949113 WHERE `page_id` = @rares AND `item_id` IN ('1000001395','1000001919','1000002469','1000003586','1000004144','1000006056','1000007588','1000008124','1000008841','1000010428','2000017','407','408','409','411','413','415','416','4723','8213');

-- Elephant (7)
INSERT INTO `catalog_pages` (`id`,`parent_id`,`caption`,`icon_image`,`min_rank`,`min_vip`,`order_num`,`page_link`,`page_layout`,`page_strings_1`,`page_strings_2`,`visible`,`enabled`)
SELECT 949114,@rares,'Elephant',28,5,0,14,'','default_3x3','','',b'1',b'1' FROM DUAL WHERE @rares IS NOT NULL
ON DUPLICATE KEY UPDATE `parent_id` = @rares, `caption` = 'Elephant', `order_num` = 14;
UPDATE `catalog_items` SET `page_id` = 949114 WHERE `page_id` = @rares AND `item_id` IN ('1000001400','1000001925','1000003584','251','256','257','8220');

-- Elephant Statue (2)
INSERT INTO `catalog_pages` (`id`,`parent_id`,`caption`,`icon_image`,`min_rank`,`min_vip`,`order_num`,`page_link`,`page_layout`,`page_strings_1`,`page_strings_2`,`visible`,`enabled`)
SELECT 949115,@rares,'Elephant Statue',28,5,0,15,'','default_3x3','','',b'1',b'1' FROM DUAL WHERE @rares IS NOT NULL
ON DUPLICATE KEY UPDATE `parent_id` = @rares, `caption` = 'Elephant Statue', `order_num` = 15;
UPDATE `catalog_items` SET `page_id` = 949115 WHERE `page_id` = @rares AND `item_id` IN ('1000002476','1000004366');

-- Fountain (13)
INSERT INTO `catalog_pages` (`id`,`parent_id`,`caption`,`icon_image`,`min_rank`,`min_vip`,`order_num`,`page_link`,`page_layout`,`page_strings_1`,`page_strings_2`,`visible`,`enabled`)
SELECT 949116,@rares,'Fountain',28,5,0,16,'','default_3x3','','',b'1',b'1' FROM DUAL WHERE @rares IS NOT NULL
ON DUPLICATE KEY UPDATE `parent_id` = @rares, `caption` = 'Fountain', `order_num` = 16;
UPDATE `catalog_items` SET `page_id` = 949116 WHERE `page_id` = @rares AND `item_id` IN ('1000001392','1000001917','1000002467','1000003574','1000006733','1000009235','1000009236','252','258','259','260','30724','8212');

-- Giant Pillow (9)
INSERT INTO `catalog_pages` (`id`,`parent_id`,`caption`,`icon_image`,`min_rank`,`min_vip`,`order_num`,`page_link`,`page_layout`,`page_strings_1`,`page_strings_2`,`visible`,`enabled`)
SELECT 949117,@rares,'Giant Pillow',28,5,0,17,'','default_3x3','','',b'1',b'1' FROM DUAL WHERE @rares IS NOT NULL
ON DUPLICATE KEY UPDATE `parent_id` = @rares, `caption` = 'Giant Pillow', `order_num` = 17;
UPDATE `catalog_items` SET `page_id` = 949117 WHERE `page_id` = @rares AND `item_id` IN ('1000001403','1000001929','368','369','371','374','375','376','377');

-- Hedgehog (15)
INSERT INTO `catalog_pages` (`id`,`parent_id`,`caption`,`icon_image`,`min_rank`,`min_vip`,`order_num`,`page_link`,`page_layout`,`page_strings_1`,`page_strings_2`,`visible`,`enabled`)
SELECT 949118,@rares,'Hedgehog',28,5,0,18,'','default_3x3','','',b'1',b'1' FROM DUAL WHERE @rares IS NOT NULL
ON DUPLICATE KEY UPDATE `parent_id` = @rares, `caption` = 'Hedgehog', `order_num` = 18;
UPDATE `catalog_items` SET `page_id` = 949118 WHERE `page_id` = @rares AND `item_id` IN ('1000001588','1000001589','1000001590','1000001591','1000001592','1000001593','1000001594','1000001595','1000001596','1000001597','1000001598','1000001599','1000001600','1000001601','1000002115');

-- Hippo (25)
INSERT INTO `catalog_pages` (`id`,`parent_id`,`caption`,`icon_image`,`min_rank`,`min_vip`,`order_num`,`page_link`,`page_layout`,`page_strings_1`,`page_strings_2`,`visible`,`enabled`)
SELECT 949119,@rares,'Hippo',28,5,0,19,'','default_3x3','','',b'1',b'1' FROM DUAL WHERE @rares IS NOT NULL
ON DUPLICATE KEY UPDATE `parent_id` = @rares, `caption` = 'Hippo', `order_num` = 19;
UPDATE `catalog_items` SET `page_id` = 949119 WHERE `page_id` = @rares AND `item_id` IN ('1000000663','1000000664','1000000665','1000000666','1000000667','1000000668','1000000669','1000000670','1000008605','540012','540013','540014','540015','540016','540017','540018','7779','7781','7782','7783','7784','7785','7786','7789','7790');

-- Ice Cream Machine (2)
INSERT INTO `catalog_pages` (`id`,`parent_id`,`caption`,`icon_image`,`min_rank`,`min_vip`,`order_num`,`page_link`,`page_layout`,`page_strings_1`,`page_strings_2`,`visible`,`enabled`)
SELECT 949120,@rares,'Ice Cream Machine',28,5,0,20,'','default_3x3','','',b'1',b'1' FROM DUAL WHERE @rares IS NOT NULL
ON DUPLICATE KEY UPDATE `parent_id` = @rares, `caption` = 'Ice Cream Machine', `order_num` = 20;
UPDATE `catalog_items` SET `page_id` = 949120 WHERE `page_id` = @rares AND `item_id` IN ('1000006060','1000009447');

-- Ice Cream Maker (16)
INSERT INTO `catalog_pages` (`id`,`parent_id`,`caption`,`icon_image`,`min_rank`,`min_vip`,`order_num`,`page_link`,`page_layout`,`page_strings_1`,`page_strings_2`,`visible`,`enabled`)
SELECT 949121,@rares,'Ice Cream Maker',28,5,0,21,'','default_3x3','','',b'1',b'1' FROM DUAL WHERE @rares IS NOT NULL
ON DUPLICATE KEY UPDATE `parent_id` = @rares, `caption` = 'Ice Cream Maker', `order_num` = 21;
UPDATE `catalog_items` SET `page_id` = 949121 WHERE `page_id` = @rares AND `item_id` IN ('1000001399','1000001924','1000002475','1000003573','1000006058','1000006059','417','418','419','421','422','423','424','425','426','8215');

-- Laser Portal (14)
INSERT INTO `catalog_pages` (`id`,`parent_id`,`caption`,`icon_image`,`min_rank`,`min_vip`,`order_num`,`page_link`,`page_layout`,`page_strings_1`,`page_strings_2`,`visible`,`enabled`)
SELECT 949122,@rares,'Laser Portal',28,5,0,22,'','default_3x3','','',b'1',b'1' FROM DUAL WHERE @rares IS NOT NULL
ON DUPLICATE KEY UPDATE `parent_id` = @rares, `caption` = 'Laser Portal', `order_num` = 22;
UPDATE `catalog_items` SET `page_id` = 949122 WHERE `page_id` = @rares AND `item_id` IN ('1000001404','1000001930','1000002480','1000003588','1000009448','339','340','341','342','343','344','345','346','8219');

-- Marquee (15)
INSERT INTO `catalog_pages` (`id`,`parent_id`,`caption`,`icon_image`,`min_rank`,`min_vip`,`order_num`,`page_link`,`page_layout`,`page_strings_1`,`page_strings_2`,`visible`,`enabled`)
SELECT 949123,@rares,'Marquee',28,5,0,23,'','default_3x3','','',b'1',b'1' FROM DUAL WHERE @rares IS NOT NULL
ON DUPLICATE KEY UPDATE `parent_id` = @rares, `caption` = 'Marquee', `order_num` = 23;
UPDATE `catalog_items` SET `page_id` = 949123 WHERE `page_id` = @rares AND `item_id` IN ('1000001390','1000001915','1000002465','1000003583','378','379','380','381','382','383','384','385','386','387','8221');

-- Oriental Screen (13)
INSERT INTO `catalog_pages` (`id`,`parent_id`,`caption`,`icon_image`,`min_rank`,`min_vip`,`order_num`,`page_link`,`page_layout`,`page_strings_1`,`page_strings_2`,`visible`,`enabled`)
SELECT 949124,@rares,'Oriental Screen',28,5,0,24,'','default_3x3','','',b'1',b'1' FROM DUAL WHERE @rares IS NOT NULL
ON DUPLICATE KEY UPDATE `parent_id` = @rares, `caption` = 'Oriental Screen', `order_num` = 24;
UPDATE `catalog_items` SET `page_id` = 949124 WHERE `page_id` = @rares AND `item_id` IN ('1000001394','1000001918','1000002468','1000003580','388','389','390','391','392','394','395','396','397');

-- Panda (15)
INSERT INTO `catalog_pages` (`id`,`parent_id`,`caption`,`icon_image`,`min_rank`,`min_vip`,`order_num`,`page_link`,`page_layout`,`page_strings_1`,`page_strings_2`,`visible`,`enabled`)
SELECT 949125,@rares,'Panda',28,5,0,25,'','default_3x3','','',b'1',b'1' FROM DUAL WHERE @rares IS NOT NULL
ON DUPLICATE KEY UPDATE `parent_id` = @rares, `caption` = 'Panda', `order_num` = 25;
UPDATE `catalog_items` SET `page_id` = 949125 WHERE `page_id` = @rares AND `item_id` IN ('1000002714','1000002715','1000002716','1000002717','1000002718','1000002719','1000002720','1000002721','1000002722','1000002723','1000002724','1000002725','1000002726','1000002727','1000003211');

-- Parasol (13)
INSERT INTO `catalog_pages` (`id`,`parent_id`,`caption`,`icon_image`,`min_rank`,`min_vip`,`order_num`,`page_link`,`page_layout`,`page_strings_1`,`page_strings_2`,`visible`,`enabled`)
SELECT 949126,@rares,'Parasol',28,5,0,26,'','default_3x3','','',b'1',b'1' FROM DUAL WHERE @rares IS NOT NULL
ON DUPLICATE KEY UPDATE `parent_id` = @rares, `caption` = 'Parasol', `order_num` = 26;
UPDATE `catalog_items` SET `page_id` = 949126 WHERE `page_id` = @rares AND `item_id` IN ('1000001396','1000001920','1000002470','1000002730','1000003572','1000003809','1000004365','1000009446','264','265','266','312','8209');

-- Pillow (7)
INSERT INTO `catalog_pages` (`id`,`parent_id`,`caption`,`icon_image`,`min_rank`,`min_vip`,`order_num`,`page_link`,`page_layout`,`page_strings_1`,`page_strings_2`,`visible`,`enabled`)
SELECT 949127,@rares,'Pillow',28,5,0,27,'','default_3x3','','',b'1',b'1' FROM DUAL WHERE @rares IS NOT NULL
ON DUPLICATE KEY UPDATE `parent_id` = @rares, `caption` = 'Pillow', `order_num` = 27;
UPDATE `catalog_items` SET `page_id` = 949127 WHERE `page_id` = @rares AND `item_id` IN ('1000002479','1000003579','2000018','2000019','372','373','8218');

-- Powered Fan (16)
INSERT INTO `catalog_pages` (`id`,`parent_id`,`caption`,`icon_image`,`min_rank`,`min_vip`,`order_num`,`page_link`,`page_layout`,`page_strings_1`,`page_strings_2`,`visible`,`enabled`)
SELECT 949128,@rares,'Powered Fan',28,5,0,28,'','default_3x3','','',b'1',b'1' FROM DUAL WHERE @rares IS NOT NULL
ON DUPLICATE KEY UPDATE `parent_id` = @rares, `caption` = 'Powered Fan', `order_num` = 28;
UPDATE `catalog_items` SET `page_id` = 949128 WHERE `page_id` = @rares AND `item_id` IN ('1000001402','1000001928','1000002478','1000003589','1000004367','1000009449','427','428','429','430','431','433','434','435','436','8214');

-- Punching Bag (7)
INSERT INTO `catalog_pages` (`id`,`parent_id`,`caption`,`icon_image`,`min_rank`,`min_vip`,`order_num`,`page_link`,`page_layout`,`page_strings_1`,`page_strings_2`,`visible`,`enabled`)
SELECT 949129,@rares,'Punching Bag',28,5,0,29,'','default_3x3','','',b'1',b'1' FROM DUAL WHERE @rares IS NOT NULL
ON DUPLICATE KEY UPDATE `parent_id` = @rares, `caption` = 'Punching Bag', `order_num` = 29;
UPDATE `catalog_items` SET `page_id` = 949129 WHERE `page_id` = @rares AND `item_id` IN ('1000000608','1000000609','1000000610','1000000611','1000000612','1000000613','1000000614');

-- Road Barrier (4)
INSERT INTO `catalog_pages` (`id`,`parent_id`,`caption`,`icon_image`,`min_rank`,`min_vip`,`order_num`,`page_link`,`page_layout`,`page_strings_1`,`page_strings_2`,`visible`,`enabled`)
SELECT 949130,@rares,'Road Barrier',28,5,0,30,'','default_3x3','','',b'1',b'1' FROM DUAL WHERE @rares IS NOT NULL
ON DUPLICATE KEY UPDATE `parent_id` = @rares, `caption` = 'Road Barrier', `order_num` = 30;
UPDATE `catalog_items` SET `page_id` = 949130 WHERE `page_id` = @rares AND `item_id` IN ('1000001405','1000001931','1000002481','1000003581');

-- Sea Otter (15)
INSERT INTO `catalog_pages` (`id`,`parent_id`,`caption`,`icon_image`,`min_rank`,`min_vip`,`order_num`,`page_link`,`page_layout`,`page_strings_1`,`page_strings_2`,`visible`,`enabled`)
SELECT 949131,@rares,'Sea Otter',28,5,0,31,'','default_3x3','','',b'1',b'1' FROM DUAL WHERE @rares IS NOT NULL
ON DUPLICATE KEY UPDATE `parent_id` = @rares, `caption` = 'Sea Otter', `order_num` = 31;
UPDATE `catalog_items` SET `page_id` = 949131 WHERE `page_id` = @rares AND `item_id` IN ('1000007121','1000007122','1000007123','1000007124','1000007125','1000007126','1000007127','1000007128','1000007129','1000007130','1000007131','1000007132','1000007133','1000007134','1000008580');

-- Seal (15)
INSERT INTO `catalog_pages` (`id`,`parent_id`,`caption`,`icon_image`,`min_rank`,`min_vip`,`order_num`,`page_link`,`page_layout`,`page_strings_1`,`page_strings_2`,`visible`,`enabled`)
SELECT 949132,@rares,'Seal',28,5,0,32,'','default_3x3','','',b'1',b'1' FROM DUAL WHERE @rares IS NOT NULL
ON DUPLICATE KEY UPDATE `parent_id` = @rares, `caption` = 'Seal', `order_num` = 32;
UPDATE `catalog_items` SET `page_id` = 949132 WHERE `page_id` = @rares AND `item_id` IN ('1000003243','1000003244','1000003245','1000003246','1000003247','1000003248','1000003249','1000003250','1000003251','1000003252','1000003253','1000003254','1000003255','1000003256','1000003935');

-- Sleeping Bag (15)
INSERT INTO `catalog_pages` (`id`,`parent_id`,`caption`,`icon_image`,`min_rank`,`min_vip`,`order_num`,`page_link`,`page_layout`,`page_strings_1`,`page_strings_2`,`visible`,`enabled`)
SELECT 949133,@rares,'Sleeping Bag',28,5,0,33,'','default_3x3','','',b'1',b'1' FROM DUAL WHERE @rares IS NOT NULL
ON DUPLICATE KEY UPDATE `parent_id` = @rares, `caption` = 'Sleeping Bag', `order_num` = 33;
UPDATE `catalog_items` SET `page_id` = 949133 WHERE `page_id` = @rares AND `item_id` IN ('1000001921','1000002472','1000003587','1000006398','671','672','673','674','676','677','678','679','680','681','8211');

-- Sloth (14)
INSERT INTO `catalog_pages` (`id`,`parent_id`,`caption`,`icon_image`,`min_rank`,`min_vip`,`order_num`,`page_link`,`page_layout`,`page_strings_1`,`page_strings_2`,`visible`,`enabled`)
SELECT 949134,@rares,'Sloth',28,5,0,34,'','default_3x3','','',b'1',b'1' FROM DUAL WHERE @rares IS NOT NULL
ON DUPLICATE KEY UPDATE `parent_id` = @rares, `caption` = 'Sloth', `order_num` = 34;
UPDATE `catalog_items` SET `page_id` = 949134 WHERE `page_id` = @rares AND `item_id` IN ('1000000813','1000000814','1000000815','1000000816','1000000817','1000000818','1000000819','1000000820','1000000821','1000000822','1000000823','1000000824','1000000825','1000000826');

-- Smoke Machine (15)
INSERT INTO `catalog_pages` (`id`,`parent_id`,`caption`,`icon_image`,`min_rank`,`min_vip`,`order_num`,`page_link`,`page_layout`,`page_strings_1`,`page_strings_2`,`visible`,`enabled`)
SELECT 949135,@rares,'Smoke Machine',28,5,0,35,'','default_3x3','','',b'1',b'1' FROM DUAL WHERE @rares IS NOT NULL
ON DUPLICATE KEY UPDATE `parent_id` = @rares, `caption` = 'Smoke Machine', `order_num` = 35;
UPDATE `catalog_items` SET `page_id` = 949135 WHERE `page_id` = @rares AND `item_id` IN ('1000001391','1000001916','1000002466','1000002729','1000003582','348','349','350','351','353','354','355','356','357','8222');

-- Spaceship Door (14)
INSERT INTO `catalog_pages` (`id`,`parent_id`,`caption`,`icon_image`,`min_rank`,`min_vip`,`order_num`,`page_link`,`page_layout`,`page_strings_1`,`page_strings_2`,`visible`,`enabled`)
SELECT 949136,@rares,'Spaceship Door',28,5,0,36,'','default_3x3','','',b'1',b'1' FROM DUAL WHERE @rares IS NOT NULL
ON DUPLICATE KEY UPDATE `parent_id` = @rares, `caption` = 'Spaceship Door', `order_num` = 36;
UPDATE `catalog_items` SET `page_id` = 949136 WHERE `page_id` = @rares AND `item_id` IN ('1000001401','1000001927','1000002477','1000003577','358','359','360','361','363','364','365','366','367','8210');

-- Throne (6)
INSERT INTO `catalog_pages` (`id`,`parent_id`,`caption`,`icon_image`,`min_rank`,`min_vip`,`order_num`,`page_link`,`page_layout`,`page_strings_1`,`page_strings_2`,`visible`,`enabled`)
SELECT 949137,@rares,'Throne',28,5,0,37,'','default_3x3','','',b'1',b'1' FROM DUAL WHERE @rares IS NOT NULL
ON DUPLICATE KEY UPDATE `parent_id` = @rares, `caption` = 'Throne', `order_num` = 37;
UPDATE `catalog_items` SET `page_id` = 949137 WHERE `page_id` = @rares AND `item_id` IN ('1000000589','1000002446','1000003585','202','4702','4724');

-- Tiger Cub (15)
INSERT INTO `catalog_pages` (`id`,`parent_id`,`caption`,`icon_image`,`min_rank`,`min_vip`,`order_num`,`page_link`,`page_layout`,`page_strings_1`,`page_strings_2`,`visible`,`enabled`)
SELECT 949138,@rares,'Tiger Cub',28,5,0,38,'','default_3x3','','',b'1',b'1' FROM DUAL WHERE @rares IS NOT NULL
ON DUPLICATE KEY UPDATE `parent_id` = @rares, `caption` = 'Tiger Cub', `order_num` = 38;
UPDATE `catalog_items` SET `page_id` = 949138 WHERE `page_id` = @rares AND `item_id` IN ('1000010158','1000010159','1000010160','1000010161','1000010162','1000010163','1000010164','1000010165','1000010166','1000010167','1000010168','1000010169','1000010170','1000010171','1000010172');

-- Any category whose lines have since moved off Rares would otherwise sit
-- there empty, so it is dropped again.
DELETE p FROM `catalog_pages` p
  LEFT JOIN `catalog_items` ci ON ci.`page_id` = p.`id`
 WHERE p.`id` BETWEEN 949101 AND 949138
   AND ci.`page_id` IS NULL;

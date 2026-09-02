-- Cartier Store: Builders divider + Cartier category + 25 furni.
-- Idempotent: re-running fully rebuilds the pages, furniture and catalog rows.

-- Parent guard (Builders exists on every env).
INSERT IGNORE INTO `catalog_pages` (`id`,`parent_id`,`caption`,`icon_image`,`min_rank`,`min_vip`,`order_num`,`page_link`,`page_layout`,`page_strings_1`,`page_strings_2`,`visible`,`enabled`)
  VALUES (912362, -1, 'Builders', 193, 2, 0, 3, '', 'default_3x3', '', '', b'1', b'1');

-- Pages: divider (non-clickable sentinel, keyed on page_link='divider' client-side) + Cartier.
DELETE FROM `catalog_pages` WHERE `id` IN (912374,912375);
INSERT INTO `catalog_pages` (`id`,`parent_id`,`caption`,`icon_image`,`min_rank`,`min_vip`,`order_num`,`page_link`,`page_layout`,`page_strings_1`,`page_strings_2`,`visible`,`enabled`) VALUES
  (912374, 912362, '-', 0, 2, 0, 1000, 'divider', 'default_3x3', '', '', b'1', b'0'),
  (912375, 912362, 'Cartier', 193, 2, 0, 1001, '', 'default_3x3', '', '', b'1', b'1');

-- Furniture definitions (id == sprite_id).
DELETE FROM `furniture` WHERE `id` IN (100010,100011,100012,100013,100014,100015,100016,100017,100018,100019,100020,100021,100022,100023,100024,100025,100026,100027,100028,100029,100030,100031,100032,100033,100034);
INSERT INTO `furniture` (`id`,`item_name`,`public_name`,`type`,`width`,`length`,`stack_height`,`can_stack`,`can_sit`,`is_walkable`,`sprite_id`,`allow_recycle`,`allow_trade`,`allow_marketplace_sell`,`allow_gift`,`allow_inventory_stack`,`interaction_type`,`behaviour_data`,`interaction_modes_count`,`is_rare`) VALUES
  (100010,'Habblet_carter_01','Cartier 01','s',1,1,0,'1','0','0',100010,'0','1','0','1','1','default',0,6,'0'),
  (100011,'Habblet_carter_02','Cartier 02','s',1,1,0,'1','0','0',100011,'0','1','0','1','1','default',0,6,'0'),
  (100012,'Habblet_carter_03','Cartier 03','s',1,1,0,'1','0','0',100012,'0','1','0','1','1','default',0,5,'0'),
  (100013,'Habblet_carter_04','Cartier 04','s',1,1,0,'1','0','0',100013,'0','1','0','1','1','default',0,5,'0'),
  (100014,'Habblet_carter_05','Cartier 05','s',1,1,0,'1','0','0',100014,'0','1','0','1','1','default',0,7,'0'),
  (100015,'Habblet_carter_06','Cartier 06','s',1,1,0,'1','0','0',100015,'0','1','0','1','1','default',0,3,'0'),
  (100016,'Habblet_carter_07','Cartier 07','s',1,1,0,'1','0','0',100016,'0','1','0','1','1','default',0,4,'0'),
  (100017,'Habblet_carter_08','Cartier 08','s',2,1,0,'1','0','0',100017,'0','1','0','1','1','default',0,2,'0'),
  (100018,'Habblet_carter_09','Cartier 09','s',1,1,0,'1','0','0',100018,'0','1','0','1','1','default',0,4,'0'),
  (100019,'Habblet_carter_10','Cartier 10','s',1,1,0,'1','0','0',100019,'0','1','0','1','1','default',0,3,'0'),
  (100020,'Habblet_carter_11','Cartier 11','s',1,1,0,'1','0','0',100020,'0','1','0','1','1','default',0,7,'0'),
  (100021,'Habblet_carter_12','Cartier 12','s',1,1,0,'1','0','0',100021,'0','1','0','1','1','default',0,12,'0'),
  (100022,'Habblet_carter_13','Cartier 13','s',1,1,0,'1','0','0',100022,'0','1','0','1','1','default',0,12,'0'),
  (100023,'Habblet_carter_14','Cartier 14','s',2,2,0,'1','0','0',100023,'0','1','0','1','1','default',0,4,'0'),
  (100024,'Habblet_carter_15','Cartier 15','s',2,1,0,'1','0','0',100024,'0','1','0','1','1','default',0,3,'0'),
  (100025,'Habblet_carter_16','Cartier 16','s',1,1,0,'1','0','0',100025,'0','1','0','1','1','default',0,2,'0'),
  (100026,'Habblet_carter_17','Cartier 17','s',1,1,0,'1','0','0',100026,'0','1','0','1','1','default',0,10,'0'),
  (100027,'Habblet_carter_18','Cartier 18','s',1,1,0,'1','0','0',100027,'0','1','0','1','1','default',0,2,'0'),
  (100028,'Habblet_carter_20','Cartier 19','s',1,1,0,'1','0','0',100028,'0','1','0','1','1','default',0,4,'0'),
  (100029,'Habblet_carter_21','Cartier 20','s',2,2,0,'1','0','0',100029,'0','1','0','1','1','default',0,3,'0'),
  (100030,'Habblet_carter_22','Cartier 21','s',1,1,0,'1','0','0',100030,'0','1','0','1','1','default',0,6,'0'),
  (100031,'Habblet_carter_23','Cartier 22','s',1,1,0,'1','0','0',100031,'0','1','0','1','1','default',0,1,'0'),
  (100032,'Habblet_carter_24','Cartier 23','s',1,1,0,'1','0','0',100032,'0','1','0','1','1','default',0,6,'0'),
  (100033,'Habblet_carter_25','Cartier 24','s',1,1,0,'1','0','0',100033,'0','1','0','1','1','default',0,4,'0'),
  (100034,'Habblet_carter_26','Cartier 25','s',2,1,0,'1','0','0',100034,'0','1','0','1','1','default',0,2,'0');

-- Catalog offers under the Cartier page (coin-only).
DELETE FROM `catalog_items` WHERE `page_id`=912375 OR `id` IN (9120010,9120011,9120012,9120013,9120014,9120015,9120016,9120017,9120018,9120019,9120020,9120021,9120022,9120023,9120024,9120025,9120026,9120027,9120028,9120029,9120030,9120031,9120032,9120033,9120034);
INSERT INTO `catalog_items` (`id`,`page_id`,`item_id`,`catalog_name`,`cost_credits`,`cost_pixels`,`cost_diamonds`,`amount`,`limited_sells`,`limited_stack`,`offer_active`,`extradata`,`badge`,`offer_id`) VALUES
  (9120010,912375,100010,'Cartier 01',3,0,0,1,0,0,'1','','',0),
  (9120011,912375,100011,'Cartier 02',3,0,0,1,0,0,'1','','',0),
  (9120012,912375,100012,'Cartier 03',3,0,0,1,0,0,'1','','',0),
  (9120013,912375,100013,'Cartier 04',3,0,0,1,0,0,'1','','',0),
  (9120014,912375,100014,'Cartier 05',3,0,0,1,0,0,'1','','',0),
  (9120015,912375,100015,'Cartier 06',3,0,0,1,0,0,'1','','',0),
  (9120016,912375,100016,'Cartier 07',3,0,0,1,0,0,'1','','',0),
  (9120017,912375,100017,'Cartier 08',5,0,0,1,0,0,'1','','',0),
  (9120018,912375,100018,'Cartier 09',3,0,0,1,0,0,'1','','',0),
  (9120019,912375,100019,'Cartier 10',3,0,0,1,0,0,'1','','',0),
  (9120020,912375,100020,'Cartier 11',3,0,0,1,0,0,'1','','',0),
  (9120021,912375,100021,'Cartier 12',3,0,0,1,0,0,'1','','',0),
  (9120022,912375,100022,'Cartier 13',3,0,0,1,0,0,'1','','',0),
  (9120023,912375,100023,'Cartier 14',9,0,0,1,0,0,'1','','',0),
  (9120024,912375,100024,'Cartier 15',5,0,0,1,0,0,'1','','',0),
  (9120025,912375,100025,'Cartier 16',3,0,0,1,0,0,'1','','',0),
  (9120026,912375,100026,'Cartier 17',3,0,0,1,0,0,'1','','',0),
  (9120027,912375,100027,'Cartier 18',3,0,0,1,0,0,'1','','',0),
  (9120028,912375,100028,'Cartier 19',3,0,0,1,0,0,'1','','',0),
  (9120029,912375,100029,'Cartier 20',9,0,0,1,0,0,'1','','',0),
  (9120030,912375,100030,'Cartier 21',3,0,0,1,0,0,'1','','',0),
  (9120031,912375,100031,'Cartier 22',3,0,0,1,0,0,'1','','',0),
  (9120032,912375,100032,'Cartier 23',3,0,0,1,0,0,'1','','',0),
  (9120033,912375,100033,'Cartier 24',3,0,0,1,0,0,'1','','',0),
  (9120034,912375,100034,'Cartier 25',5,0,0,1,0,0,'1','','',0);

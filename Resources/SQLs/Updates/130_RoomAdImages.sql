-- pixelrp: the room-ad image furni come back, in Builders > Images.
--
-- These are the four pieces that take an IMAGE URL rather than a colour or a
-- state: ads_background paints the whole room backdrop, and ads_mpu_160 /
-- ads_mpu_300 / ads_mpu_720 are the three billboard sizes. Nothing else in the
-- hotel does this. That is not a guess from the names - every ads_* bundle the
-- server serves (293 of them) was read, and exactly these four declare a
-- logicType that carries an imageUrl:
--
--     ads_background            furniture_bg   (FurnitureRoomBackgroundLogic)
--     ads_mpu_160/300/720       furniture_bb   (FurnitureRoomBillboardLogic)
--
-- Both logics extend FurnitureRoomBrandingLogic, which reads 'imageUrl' out of
-- a key/value map. background_color is deliberately NOT here: its logic is
-- furniture_background_color, a colour toner with no url.
--
-- WHY THEY VANISHED: 83 filed them on a staff 'Room Backgrounds' page, and 119
-- rebuilt the whole non-Builders catalog from the default - which has no such
-- page, so the rows went and nothing replaced them. Builders is the right home
-- precisely because 83 and 119 both refuse to touch it ("the Builders tab and
-- everything under it is never touched"), so a future catalog restore cannot
-- take them away a second time.
--
-- INTERACTION TYPE: the map the client reads only arrives if the emulator
-- serializes the item as MapDataFormat, which it does for interaction_type
-- 'background' (ItemBehaviourUtility.HydrateExtraData). A piece with the right
-- art and the wrong row reaches the client as one opaque string and its map
-- parses empty - the image silently never loads. 83's rows covered three of
-- the four; ads_mpu_160 came in with 31 and has never had one, so all four are
-- set here rather than trusting what is already there.
--
-- RANK: min_rank 2, the same gate as every other page under Builders. These
-- load an arbitrary remote image into everyone's client in the room, so the
-- Builders gate is the point, not an accident.
--
-- Pages resolved by caption, never by id - Builders and its children are
-- data-only, created live and present in no migration, so an id read out of a
-- file here would be a guess. 119 had to be rewritten for exactly that.
--
-- Idempotent: fixed page id, cleared before insert, and the order shift is
-- guarded so a re-run does not open a second gap.

SET @builders := COALESCE(
    (SELECT `id` FROM `catalog_pages` WHERE `parent_id` = -1 AND `caption` = 'Builders' LIMIT 1),
    912362);
SET @information := (SELECT `id` FROM `catalog_pages`
    WHERE `parent_id` = @builders AND `caption` = 'Information' LIMIT 1);

-- Images sits directly below Information. If Information is not there under
-- that name, fall back to the end of the tab rather than to slot 1, where it
-- would silently jump the whole group.
SET @after := COALESCE(
    (SELECT `order_num` FROM `catalog_pages` WHERE `id` = @information),
    (SELECT MAX(`order_num`) FROM `catalog_pages` WHERE `parent_id` = @builders),
    0);

-- Open the slot only if something is standing in it. 95 renumbers the children
-- of Construction, Customs and Designer alphabetically and deliberately leaves
-- the Builders tab's own groups alone, so this order is manual and stays put.
SET @needs_gap := (SELECT COUNT(*) FROM `catalog_pages`
    WHERE `parent_id` = @builders AND `order_num` = @after + 1 AND `id` <> 946000);

UPDATE `catalog_pages` SET `order_num` = `order_num` + 1
    WHERE `parent_id` = @builders
      AND `order_num` > @after
      AND `id` <> 946000
      AND @needs_gap > 0;

DELETE FROM `catalog_items` WHERE `page_id` = 946000;
DELETE FROM `catalog_pages` WHERE `id` = 946000;

INSERT INTO `catalog_pages`
    (`id`,`parent_id`,`caption`,`icon_image`,`min_rank`,`min_vip`,`order_num`,`page_link`,
     `page_layout`,`page_strings_1`,`page_strings_2`,`visible`,`enabled`)
VALUES
    (946000, @builders, 'Images', 193, 2, 0, @after + 1, '', 'default_3x3', '', '', b'1', b'1');

-- The row that makes the image dialog work at all. Named explicitly rather
-- than matched on interaction_type, because ads_mpu_160 does not have it yet -
-- selecting on the thing this statement sets would skip the one piece that
-- needs it.
UPDATE `furniture`
    SET `interaction_type` = 'background'
    WHERE `item_name` IN ('ads_background', 'ads_mpu_160', 'ads_mpu_300', 'ads_mpu_720');

-- Anything that already had a catalog row moves rather than being duplicated.
UPDATE `catalog_items` `ci`
    JOIN `furniture` `f` ON `f`.`id` = `ci`.`item_id`
    SET `ci`.`page_id` = 946000
    WHERE `f`.`interaction_type` = 'background';

-- Free, like the rest of the catalog since 90_FreeCatalog. catalog_name is for
-- the database's own readers; the client draws the name from FurnitureData.
INSERT INTO `catalog_items`
    (`page_id`,`item_id`,`catalog_name`,`cost_credits`,`cost_pixels`,`cost_diamonds`,
     `amount`,`limited_sells`,`limited_stack`,`offer_active`,`extradata`,`badge`,`offer_id`)
SELECT 946000, `f`.`id`,
       CASE `f`.`item_name`
           WHEN 'ads_background' THEN 'Room Background'
           WHEN 'ads_mpu_160'    THEN 'Billboard (Small)'
           WHEN 'ads_mpu_300'    THEN 'Billboard (Medium)'
           WHEN 'ads_mpu_720'    THEN 'Billboard (Large)'
           ELSE `f`.`item_name`
       END,
       0, 0, 0, 1, 0, 0, '1', '', '', -1
    FROM `furniture` `f`
    WHERE `f`.`interaction_type` = 'background'
      AND NOT EXISTS (SELECT 1 FROM `catalog_items` `ci` WHERE `ci`.`item_id` = `f`.`id`);

-- Where the pages resolved to. A NULL information id means the tab has no page
-- by that name and Images went to the end of the group instead.
SELECT @builders AS `builders`, @information AS `information`, @after AS `after_order`;

-- The Builders tab as it now reads, top to bottom. Images should sit directly
-- below Information and nothing should share an order_num.
SELECT `order_num`, `caption`, `id`
    FROM `catalog_pages`
    WHERE `parent_id` = @builders
    ORDER BY `order_num`, `caption`;

-- Every image-url piece and the page it now sits on. Four rows, all reading
-- Images. A piece with no page has furniture but no catalog row; one missing
-- entirely is not in the furniture table under that name.
SELECT `f`.`item_name`, `f`.`public_name`, `f`.`interaction_type`, `p`.`caption` AS `page`
    FROM `furniture` `f`
    LEFT JOIN `catalog_items` `ci` ON `ci`.`item_id` = `f`.`id`
    LEFT JOIN `catalog_pages` `p` ON `p`.`id` = `ci`.`page_id`
    WHERE `f`.`item_name` IN ('ads_background', 'ads_mpu_160', 'ads_mpu_300', 'ads_mpu_720')
    ORDER BY `f`.`item_name`;

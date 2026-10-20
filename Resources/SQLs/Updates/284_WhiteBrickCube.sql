-- PixelRP: the White Brick Cube, in Builders > Customs > Bricks.
--
-- A 1x1, one-high cube in the brick of Bsstonino's White Brick Block
-- (bsstonino_furni706, 2x1). It stacks and sits side by side with no seam in
-- any direction, so it is exactly 1.0 high and stacks on itself. Two states,
-- as the 2x1: 0 white, 1 grey, switched by clicking.
--
-- The rendering half is the .nitro bundle, the icon and the FurnitureData
-- entry under nitro/overrides/, drawn by docker/nitro/make-white-brick-cube.py;
-- the rows below are inert without them.
--
-- The Bricks page is found by its path, Builders > Customs > Bricks, or failing
-- that whichever page sells Block 1 (brick_block1) - both are 944006 today
-- (94_CustomsFurni).
--
-- Free, like the rest of the catalog since 90_FreeCatalog.
--
-- Idempotent: a fixed furniture id, cleared before insert.

SET @builders := (SELECT `id` FROM `catalog_pages` WHERE `parent_id` = -1 AND `caption` = 'Builders' LIMIT 1);
SET @customs := (SELECT `id` FROM `catalog_pages` WHERE `parent_id` = @builders AND `caption` = 'Customs'
    ORDER BY `id` LIMIT 1);
SET @bricks := COALESCE(
    (SELECT `id` FROM `catalog_pages` WHERE `parent_id` = @customs AND `caption` = 'Bricks'
        ORDER BY `id` LIMIT 1),
    (SELECT ci.`page_id` FROM `catalog_items` ci
        JOIN `furniture` f ON CAST(f.`id` AS CHAR) = ci.`item_id`
        WHERE f.`item_name` = 'brick_block1' ORDER BY ci.`id` LIMIT 1));

DELETE FROM `catalog_items` WHERE `item_id` = '111001';
DELETE FROM `furniture` WHERE `id` = 111001;

-- id == sprite_id, the convention every custom line here follows. Stackable,
-- 1.0 high to match the art, two interaction modes for the two colours.
INSERT INTO `furniture` (`id`,`item_name`,`public_name`,`type`,`width`,`length`,`stack_height`,
    `can_stack`,`can_sit`,`is_walkable`,`sprite_id`,`allow_recycle`,`allow_trade`,
    `allow_marketplace_sell`,`allow_gift`,`allow_inventory_stack`,`interaction_type`,
    `behaviour_data`,`interaction_modes_count`,`is_rare`) VALUES
    (111001,'white_brick_cube','White Brick Cube','s',1,1,1.0,'1','0','0',111001,'0','1','0','1','1','default',0,2,'0');

-- Guarded on the page resolving: a DB without it gets the furni and no offer,
-- rather than an offer on page NULL.
INSERT INTO `catalog_items` (`page_id`,`item_id`,`catalog_name`,`cost_credits`,`cost_pixels`,
    `cost_diamonds`,`amount`,`limited_sells`,`limited_stack`,`offer_active`,`extradata`,`badge`,`offer_id`)
SELECT @bricks,'111001','White Brick Cube',0,0,0,1,0,0,'1','','',-1
    FROM DUAL WHERE @bricks IS NOT NULL;

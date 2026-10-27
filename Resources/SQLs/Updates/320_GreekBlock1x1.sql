-- PixelRP: Greek Block, in Builders > Themes > Habbolympics beside the Greek Pillow.
--
-- The Greek Pillow (greek_block, 2x1) on one tile. Blocks side by side join
-- into one slab with no seams in any direction; both turns are the same.
--
-- The rendering half is the .nitro bundle, the icon and the FurnitureData /
-- ExternalTexts entries under nitro/overrides/, drawn by
-- docker/nitro/make-greek-block-1x1.py; the rows below are inert without them.
--
-- As the Greek Pillow: stack height 0.5, things stack on it, no sitting or
-- walking. Free, like the rest of the catalog since 90_FreeCatalog.
--
-- The Habbolympics page is found by its path, or failing that whichever page
-- sells the Greek Pillow (greek_block).
--
-- Idempotent: a fixed furniture id, cleared before insert.

SET @builders := (SELECT `id` FROM `catalog_pages` WHERE `parent_id` = -1 AND `caption` = 'Builders' LIMIT 1);
SET @themes := (SELECT `id` FROM `catalog_pages` WHERE `parent_id` = @builders AND `caption` = 'Themes'
    ORDER BY `id` LIMIT 1);
SET @habbolympics := COALESCE(
    (SELECT `id` FROM `catalog_pages` WHERE `parent_id` = @themes AND `caption` = 'Habbolympics'
        ORDER BY `id` LIMIT 1),
    (SELECT ci.`page_id` FROM `catalog_items` ci
        JOIN `furniture` f ON CAST(f.`id` AS CHAR) = ci.`item_id`
        WHERE f.`item_name` = 'greek_block' ORDER BY ci.`id` LIMIT 1));

DELETE FROM `catalog_items` WHERE `item_id` = '111006';
DELETE FROM `furniture` WHERE `id` = 111006;

-- id == sprite_id, the convention every custom line here follows.
INSERT INTO `furniture` (`id`,`item_name`,`public_name`,`type`,`width`,`length`,`stack_height`,
    `can_stack`,`can_sit`,`is_walkable`,`sprite_id`,`allow_recycle`,`allow_trade`,
    `allow_marketplace_sell`,`allow_gift`,`allow_inventory_stack`,`interaction_type`,
    `behaviour_data`,`interaction_modes_count`,`is_rare`) VALUES
    (111006,'greek_block_1x1','Greek Block','s',1,1,0.5,'1','0','0',111006,'1','1','1','1','1',
     'default',0,1,'0');

-- Guarded on the page resolving: a DB without it gets the furni and no offer,
-- rather than an offer on page NULL.
INSERT INTO `catalog_items` (`page_id`,`item_id`,`catalog_name`,`cost_credits`,`cost_pixels`,
    `cost_diamonds`,`amount`,`limited_sells`,`limited_stack`,`offer_active`,`extradata`,`badge`,`offer_id`)
SELECT @habbolympics,'111006','Greek Block',0,0,0,1,0,0,'1','','',-1
    FROM DUAL WHERE @habbolympics IS NOT NULL;

-- PixelRP: the Vapes and Cigarettes Stand, in Builders > Themes > Supermarket.
--
-- One furni - Habbo's Stationary Stand (market_c19_stationary) restocked with
-- vapes, e-liquids and cigarette packs - with its own art, so its own
-- classname, market_c19_vapestand. It sells beside the stand it is drawn
-- from: the Supermarket page under Builders > Themes, found by that path the
-- way 182_MiscSorted finds it, or failing that whichever page sells the
-- Stationary Stand.
--
-- The rendering half is the .nitro bundle, the icon and the FurnitureData
-- entry under nitro/overrides/; the rows below are inert without them.
--
-- Free, like the rest of the catalog since 90_FreeCatalog.
--
-- Idempotent: a fixed furniture id, cleared before insert.

SET @builders := (SELECT `id` FROM `catalog_pages` WHERE `parent_id` = -1 AND `caption` = 'Builders' LIMIT 1);
SET @themes := (SELECT `id` FROM `catalog_pages` WHERE `parent_id` = @builders AND `caption` = 'Themes'
    AND `visible` = b'1' AND `enabled` = b'1' ORDER BY `id` LIMIT 1);
SET @supermarket := COALESCE(
    (SELECT `id` FROM `catalog_pages` WHERE `parent_id` = @themes AND `caption` = 'Supermarket'
        AND `visible` = b'1' AND `enabled` = b'1' ORDER BY `id` LIMIT 1),
    (SELECT ci.`page_id` FROM `catalog_items` ci
        JOIN `furniture` f ON CAST(f.`id` AS CHAR) = ci.`item_id`
        WHERE f.`item_name` = 'market_c19_stationary' ORDER BY ci.`id` LIMIT 1));

DELETE FROM `catalog_items` WHERE `item_id` = '111000';
DELETE FROM `furniture` WHERE `id` = 111000;

-- id == sprite_id, the convention every custom line here follows. Size, height
-- and stacking as the Stationary Stand it is drawn from.
INSERT INTO `furniture` (`id`,`item_name`,`public_name`,`type`,`width`,`length`,`stack_height`,
    `can_stack`,`can_sit`,`is_walkable`,`sprite_id`,`allow_recycle`,`allow_trade`,
    `allow_marketplace_sell`,`allow_gift`,`allow_inventory_stack`,`interaction_type`,
    `behaviour_data`,`interaction_modes_count`,`is_rare`) VALUES
    (111000,'market_c19_vapestand','Vapes and Cigarettes Stand','s',2,1,1.0,'1','0','0',111000,'0','1','0','1','1','default',0,1,'0');

-- Guarded on the page resolving: a DB without it gets the furni and no offer,
-- rather than an offer on page NULL.
INSERT INTO `catalog_items` (`page_id`,`item_id`,`catalog_name`,`cost_credits`,`cost_pixels`,
    `cost_diamonds`,`amount`,`limited_sells`,`limited_stack`,`offer_active`,`extradata`,`badge`,`offer_id`)
SELECT @supermarket,'111000','Vapes and Cigarettes Stand',0,0,0,1,0,0,'1','','',-1
    FROM DUAL WHERE @supermarket IS NOT NULL;

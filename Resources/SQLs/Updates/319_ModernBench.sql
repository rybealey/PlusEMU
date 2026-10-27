-- PixelRP: Modern Bench, in Builders > Customs > City & Roleplay > City Life.
--
-- The Modern Bus Stop's bench on its own: a 2x1 seat of oak slats and a slatted
-- backrest on slim steel legs. It turns all four ways (the bundle draws each).
--
-- The rendering half is the .nitro bundle, the icon and the FurnitureData /
-- ExternalTexts entries under nitro/overrides/, drawn by
-- docker/nitro/make-modern-bench.py; the rows below are inert without them.
--
-- A seat as most two-tile benches here: stack height 1.0, sat on at its stack
-- height (sit_height left at -1). Free, like the rest of the catalog since
-- 90_FreeCatalog.
--
-- The City Life page is found as in 317_ModernBusStop: by its path, or failing
-- that whichever page sells the City vacuum cleaner (hcity_aspirateur).
--
-- Idempotent: a fixed furniture id, cleared before insert.

SET @builders := (SELECT `id` FROM `catalog_pages` WHERE `parent_id` = -1 AND `caption` = 'Builders' LIMIT 1);
SET @customs := (SELECT `id` FROM `catalog_pages` WHERE `parent_id` = @builders AND `caption` = 'Customs'
    ORDER BY `id` LIMIT 1);
SET @city := (SELECT `id` FROM `catalog_pages` WHERE `parent_id` = @customs AND `caption` = 'City & Roleplay'
    ORDER BY `id` LIMIT 1);
SET @city_life := COALESCE(
    (SELECT `id` FROM `catalog_pages` WHERE `parent_id` = @city AND `caption` = 'City Life'
        ORDER BY `id` LIMIT 1),
    (SELECT ci.`page_id` FROM `catalog_items` ci
        JOIN `furniture` f ON CAST(f.`id` AS CHAR) = ci.`item_id`
        WHERE f.`item_name` = 'hcity_aspirateur' ORDER BY ci.`id` LIMIT 1));

DELETE FROM `catalog_items` WHERE `item_id` = '111005';
DELETE FROM `furniture` WHERE `id` = 111005;

-- id == sprite_id, the convention every custom line here follows.
INSERT INTO `furniture` (`id`,`item_name`,`public_name`,`type`,`width`,`length`,`stack_height`,
    `can_stack`,`can_sit`,`is_walkable`,`sprite_id`,`allow_recycle`,`allow_trade`,
    `allow_marketplace_sell`,`allow_gift`,`allow_inventory_stack`,`interaction_type`,
    `behaviour_data`,`interaction_modes_count`,`is_rare`) VALUES
    (111005,'modern_bench','Modern Bench','s',2,1,1.0,'1','1','0',111005,'1','1','1','1','1',
     'default',0,1,'0');

-- Guarded on the page resolving: a DB without it gets the furni and no offer,
-- rather than an offer on page NULL.
INSERT INTO `catalog_items` (`page_id`,`item_id`,`catalog_name`,`cost_credits`,`cost_pixels`,
    `cost_diamonds`,`amount`,`limited_sells`,`limited_stack`,`offer_active`,`extradata`,`badge`,`offer_id`)
SELECT @city_life,'111005','Modern Bench',0,0,0,1,0,0,'1','','',-1
    FROM DUAL WHERE @city_life IS NOT NULL;

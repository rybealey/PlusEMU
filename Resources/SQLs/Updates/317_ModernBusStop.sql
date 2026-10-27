-- PixelRP: Modern Bus Stop, in Builders > Customs > City & Roleplay > City Life.
--
-- Habbo's Haunted bus stop (hween_c15_busstop, 2x1) made modern as its own furni;
-- the haunted one is left as it is. White frame, clean glass, a transit map in
-- place of the ghost poster, a new wooden bench on steel legs. Three states,
-- switched by clicking: 0 clear glass, 1 smoked glass, 2 frosted glass with fog.
--
-- The rendering half is the .nitro bundle, the icon and the FurnitureData /
-- ExternalTexts entries under nitro/overrides/, drawn by
-- docker/nitro/make-modern-bus-stop.py; the rows below are inert without them.
--
-- A seat over both tiles as the haunted one: the same size, stack height and
-- sit height (1), and the same trade, gift and Marketplace flags. Free, like the
-- rest of the catalog since 90_FreeCatalog.
--
-- The City Life page is found by its path, or failing that whichever page sells
-- the City vacuum cleaner (hcity_aspirateur) - both are 965122 on beta.
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

DELETE FROM `catalog_items` WHERE `item_id` = '111003';
DELETE FROM `furniture` WHERE `id` = 111003;

-- id == sprite_id, the convention every custom line here follows.
INSERT INTO `furniture` (`id`,`item_name`,`public_name`,`type`,`width`,`length`,`stack_height`,
    `can_stack`,`can_sit`,`is_walkable`,`sprite_id`,`allow_recycle`,`allow_trade`,
    `allow_marketplace_sell`,`allow_gift`,`allow_inventory_stack`,`interaction_type`,
    `behaviour_data`,`interaction_modes_count`,`sit_height`,`is_rare`) VALUES
    (111003,'modern_bus_stop','Modern Bus Stop','s',2,1,0,'1','1','0',111003,'1','1','1','1','1',
     'default',0,3,1,'0');

-- Guarded on the page resolving: a DB without it gets the furni and no offer,
-- rather than an offer on page NULL.
INSERT INTO `catalog_items` (`page_id`,`item_id`,`catalog_name`,`cost_credits`,`cost_pixels`,
    `cost_diamonds`,`amount`,`limited_sells`,`limited_stack`,`offer_active`,`extradata`,`badge`,`offer_id`)
SELECT @city_life,'111003','Modern Bus Stop',0,0,0,1,0,0,'1','','',-1
    FROM DUAL WHERE @city_life IS NOT NULL;

-- PixelRP: the PixelRP Bin, in Builders > Infrastructure.
--
-- One furni - a wooden crate bin, lid shut or lid open with rubbish showing -
-- sold on the page 15 made for Navigation furni, which is renamed
-- Infrastructure here: it is the page for the fixtures a city is built out of,
-- arrows and taxi signs and now bins, not only the ones that move people.
--
-- The bundle's classname is anubis_bin; its title is ours. Two states from the
-- bundle's own animations, so interaction 'default' with two modes opens and
-- shuts it on a double-click.
--
-- The rendering half is the .nitro bundle, the icon and the FurnitureData
-- entry under nitro/overrides/; the rows below are inert without them.
--
-- Free, like the rest of the catalog since 90_FreeCatalog.
--
-- Idempotent: a fixed furniture id, cleared before insert, and a rename
-- resolved by id first so a re-run after it has happened still finds the page.

SET @builders := COALESCE(
    (SELECT `id` FROM `catalog_pages` WHERE `parent_id` = -1 AND `caption` = 'Builders' LIMIT 1),
    912362);
SET @infrastructure := COALESCE(
    (SELECT `id` FROM `catalog_pages` WHERE `id` = 912363 LIMIT 1),
    (SELECT `id` FROM `catalog_pages` WHERE `parent_id` = @builders AND `caption` = 'Navigation'
        ORDER BY `id` LIMIT 1));

UPDATE `catalog_pages` SET `caption` = 'Infrastructure' WHERE `id` = @infrastructure;

DELETE FROM `catalog_items` WHERE `item_id` = '110500';
DELETE FROM `furniture` WHERE `id` = 110500;

-- id == sprite_id, the convention every custom line here follows.
INSERT INTO `furniture` (`id`,`item_name`,`public_name`,`type`,`width`,`length`,`stack_height`,
    `can_stack`,`can_sit`,`is_walkable`,`sprite_id`,`allow_recycle`,`allow_trade`,
    `allow_marketplace_sell`,`allow_gift`,`allow_inventory_stack`,`interaction_type`,
    `behaviour_data`,`interaction_modes_count`,`is_rare`) VALUES
    (110500,'anubis_bin','PixelRP Bin','s',1,1,1.0,'1','0','0',110500,'0','1','0','1','1','default',0,2,'0');

-- Guarded on the page resolving: a DB without it gets the furni and no offer,
-- rather than an offer on page NULL.
INSERT INTO `catalog_items` (`page_id`,`item_id`,`catalog_name`,`cost_credits`,`cost_pixels`,
    `cost_diamonds`,`amount`,`limited_sells`,`limited_stack`,`offer_active`,`extradata`,`badge`,`offer_id`)
SELECT @infrastructure,'110500','PixelRP Bin',0,0,0,1,0,0,'1','','',-1
    FROM DUAL WHERE @infrastructure IS NOT NULL;

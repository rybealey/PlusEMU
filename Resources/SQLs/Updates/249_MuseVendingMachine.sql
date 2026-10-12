-- PixelRP: the Muse Vending Machine, in Furni > Misc.
--
-- One furni - the Vegan Vending Machine (olympics_r16_vendingmchn) repainted
-- in The Muse's teal and stocked with Muse chip bags - with its own art, so its
-- own classname, muse_vendingmachine. Using it plays its dispense state: a bag
-- drops from the middle shelf into the pickup slot, sits there for two seconds,
-- and the machine settles back to idle (interaction 'snack_dispenser',
-- InteractorSnackDispenser). It hands nothing out - snacks are sold by The Muse.
--
-- The rendering half is the .nitro bundle, the icon and the FurnitureData
-- entry under nitro/overrides/; the rows below are inert without them.
--
-- Free, like the rest of the catalog since 90_FreeCatalog.
--
-- Idempotent: a fixed furniture id, cleared before insert.

SET @furni := (SELECT `id` FROM `catalog_pages` WHERE `parent_id` = -1 AND `page_link` = 'furni' LIMIT 1);
SET @misc := (SELECT `id` FROM `catalog_pages` WHERE `parent_id` = @furni AND `caption` = 'Misc' LIMIT 1);

DELETE FROM `catalog_items` WHERE `item_id` = '110706';
DELETE FROM `furniture` WHERE `id` = 110706;

-- id == sprite_id, the convention every custom line here follows. Size, height
-- and stacking as the Vegan machine it is drawn from.
INSERT INTO `furniture` (`id`,`item_name`,`public_name`,`type`,`width`,`length`,`stack_height`,
    `can_stack`,`can_sit`,`is_walkable`,`sprite_id`,`allow_recycle`,`allow_trade`,
    `allow_marketplace_sell`,`allow_gift`,`allow_inventory_stack`,`interaction_type`,
    `behaviour_data`,`interaction_modes_count`,`is_rare`) VALUES
    (110706,'muse_vendingmachine','Muse Vending Machine','s',1,1,2.7,'1','0','0',110706,'0','1','0','1','1','snack_dispenser',0,2,'0');

-- Guarded on the page resolving: a DB without it gets the furni and no offer,
-- rather than an offer on page NULL.
INSERT INTO `catalog_items` (`page_id`,`item_id`,`catalog_name`,`cost_credits`,`cost_pixels`,
    `cost_diamonds`,`amount`,`limited_sells`,`limited_stack`,`offer_active`,`extradata`,`badge`,`offer_id`)
SELECT @misc,'110706','Muse Vending Machine',0,0,0,1,0,0,'1','','',-1
    FROM DUAL WHERE @misc IS NOT NULL;

-- PixelRP: Judge's Gavel, in Builders > Customs > City & Roleplay > RP Props.
--
-- A dark wooden gavel with gold bands on its round block, made after the
-- Mallet and Chisel (xmas13_tools1) with the chisel gone. Turns all four ways.
--
-- The rendering half is the .nitro bundle, the icon and the FurnitureData /
-- ExternalTexts entries under nitro/overrides/, drawn by
-- docker/nitro/make-judge-gavel.py; the rows below are inert without them.
--
-- Two states that look the same at rest: every click changes the state, and
-- the bundle plays one bang on each change (the gavel lifts and strikes the
-- block), so a plain 2-mode default interaction is all it needs.
--
-- Stack height 0.3, nothing stacks on it, no sitting or walking. Free, like
-- the rest of the catalog since 90_FreeCatalog.
--
-- The RP Props page is found by its path, or failing that by its name.
--
-- Idempotent: a fixed furniture id, cleared before insert.

SET @builders := (SELECT `id` FROM `catalog_pages` WHERE `parent_id` = -1 AND `caption` = 'Builders' LIMIT 1);
SET @customs := (SELECT `id` FROM `catalog_pages` WHERE `parent_id` = @builders AND `caption` = 'Customs'
    ORDER BY `id` LIMIT 1);
SET @city_rp := (SELECT `id` FROM `catalog_pages` WHERE `parent_id` = @customs AND `caption` = 'City & Roleplay'
    ORDER BY `id` LIMIT 1);
SET @rp_props := COALESCE(
    (SELECT `id` FROM `catalog_pages` WHERE `parent_id` = @city_rp AND `caption` = 'RP Props'
        ORDER BY `id` LIMIT 1),
    (SELECT `id` FROM `catalog_pages` WHERE `caption` = 'RP Props' ORDER BY `id` LIMIT 1));

DELETE FROM `catalog_items` WHERE `item_id` = '111007';
DELETE FROM `furniture` WHERE `id` = 111007;

-- id == sprite_id, the convention every custom line here follows.
INSERT INTO `furniture` (`id`,`item_name`,`public_name`,`type`,`width`,`length`,`stack_height`,
    `can_stack`,`can_sit`,`is_walkable`,`sprite_id`,`allow_recycle`,`allow_trade`,
    `allow_marketplace_sell`,`allow_gift`,`allow_inventory_stack`,`interaction_type`,
    `behaviour_data`,`interaction_modes_count`,`is_rare`) VALUES
    (111007,'judge_gavel','Judge''s Gavel','s',1,1,0.3,'0','0','0',111007,'1','1','1','1','1',
     'default',0,2,'0');

-- Guarded on the page resolving: a DB without it gets the furni and no offer,
-- rather than an offer on page NULL.
INSERT INTO `catalog_items` (`page_id`,`item_id`,`catalog_name`,`cost_credits`,`cost_pixels`,
    `cost_diamonds`,`amount`,`limited_sells`,`limited_stack`,`offer_active`,`extradata`,`badge`,`offer_id`)
SELECT @rp_props,'111007','Judge''s Gavel',0,0,0,1,0,0,'1','','',-1
    FROM DUAL WHERE @rp_props IS NOT NULL;

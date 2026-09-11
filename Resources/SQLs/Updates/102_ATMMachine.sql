-- PixelRP: the ATM Machine, in Builders > Corporations.
--
-- A 1x1 cash machine (Wetwillies' 2012 RaGEZONE release, converted from its
-- SWF), sold on the Corporations page so a bank branch can be built out of
-- more than wall panels. It is a plain two-state multistate: clicking it
-- toggles the screen between idle and the green "ATM" display. Nothing in the
-- emulator special-cases it - no balance changes hands - so it is scenery a
-- player can switch on, the same as the release shipped it.
--
-- The rendering half is nitro/overrides/bundled/furniture/atm_moneymachine.nitro,
-- its icon under dcr/hof_furni/icons/ and the FurnitureData/ExternalTexts
-- entries in gamedata-merge/; the rows below are inert without them.
--
-- Free, like the rest of the catalog since 90_FreeCatalog.
--
-- Corporations is a data-only page (it predates these migrations and exists
-- only in the database), so it is resolved by caption. Should it ever be
-- missing, the item falls back to the Builders root rather than being
-- inserted against a page id that does not exist.
--
-- Idempotent: fixed id, cleared before insert.

SET @builders := (SELECT `id` FROM `catalog_pages` WHERE `parent_id` = -1 AND `caption` = 'Builders' LIMIT 1);
SET @corps    := (SELECT `id` FROM `catalog_pages` WHERE `parent_id` = @builders AND `caption` = 'Corporations' LIMIT 1);
SET @page     := COALESCE(@corps, @builders);

DELETE FROM `catalog_items` WHERE `item_id` = '106754';
DELETE FROM `furniture` WHERE `id` = 106754;

INSERT INTO `furniture`
    (`id`,`item_name`,`public_name`,`type`,`width`,`length`,`stack_height`,`can_stack`,`can_sit`,
     `is_walkable`,`sprite_id`,`allow_recycle`,`allow_trade`,`allow_marketplace_sell`,`allow_gift`,
     `allow_inventory_stack`,`interaction_type`,`behaviour_data`,`interaction_modes_count`,`is_rare`)
VALUES
    (106754,'atm_moneymachine','ATM Machine','s',1,1,1.0,'0','0','0',106754,'0','1','0','1','1','default',0,2,'0');

INSERT INTO `catalog_items`
    (`page_id`,`item_id`,`catalog_name`,`cost_credits`,`cost_pixels`,`cost_diamonds`,
     `amount`,`limited_sells`,`limited_stack`,`offer_active`,`extradata`,`badge`,`offer_id`)
SELECT @page, '106754', 'ATM Machine', 0, 0, 0, 1, 0, 0, '1', '', '', -1
FROM DUAL WHERE @page IS NOT NULL;

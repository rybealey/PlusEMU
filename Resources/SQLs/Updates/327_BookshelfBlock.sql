-- PixelRP: Bookshelf Block, in Furniture Sets > Classic & Antique > Classic Townhouse
-- beside the Bookshelf.
--
-- The Classic Townhouse Bookshelf (belmondfolasade_bookshelf, 1x2) on one tile.
-- Blocks side by side join into one with no seam; the same three looks,
-- clicked through: closed, open and low.
--
-- The rendering half is the .nitro bundle, the icon and the FurnitureData /
-- ExternalTexts entries under nitro/overrides/, drawn by
-- docker/nitro/make-bookshelf-1x1.py; the rows below are inert without them.
--
-- As the Bookshelf: stack height 1, things stack on it, no sitting or walking,
-- 3 looks. Free, like the rest of the catalog since 90_FreeCatalog.
--
-- The Classic Townhouse page is found by its path, or failing that whichever
-- page sells the Bookshelf (belmondfolasade_bookshelf).
--
-- Idempotent: a fixed furniture id, cleared before insert.

SET @sets := (SELECT `id` FROM `catalog_pages` WHERE `caption` = 'Furniture Sets' ORDER BY `id` LIMIT 1);
SET @classic := (SELECT `id` FROM `catalog_pages` WHERE `parent_id` = @sets AND `caption` = 'Classic & Antique'
    ORDER BY `id` LIMIT 1);
SET @townhouse := COALESCE(
    (SELECT `id` FROM `catalog_pages` WHERE `parent_id` = @classic AND `caption` = 'Classic Townhouse'
        ORDER BY `id` LIMIT 1),
    (SELECT ci.`page_id` FROM `catalog_items` ci
        JOIN `furniture` f ON CAST(f.`id` AS CHAR) = ci.`item_id`
        WHERE f.`item_name` = 'belmondfolasade_bookshelf' ORDER BY ci.`id` LIMIT 1));

DELETE FROM `catalog_items` WHERE `item_id` = '111008';
DELETE FROM `furniture` WHERE `id` = 111008;

-- id == sprite_id, the convention every custom line here follows.
INSERT INTO `furniture` (`id`,`item_name`,`public_name`,`type`,`width`,`length`,`stack_height`,
    `can_stack`,`can_sit`,`is_walkable`,`sprite_id`,`allow_recycle`,`allow_trade`,
    `allow_marketplace_sell`,`allow_gift`,`allow_inventory_stack`,`interaction_type`,
    `behaviour_data`,`interaction_modes_count`,`is_rare`) VALUES
    (111008,'belmondfolasade_bookshelf_1x1','Bookshelf Block','s',1,1,1,'1','0','0',111008,'1','1','1','1','1',
     'default',0,3,'0');

-- Guarded on the page resolving: a DB without it gets the furni and no offer,
-- rather than an offer on page NULL.
INSERT INTO `catalog_items` (`page_id`,`item_id`,`catalog_name`,`cost_credits`,`cost_pixels`,
    `cost_diamonds`,`amount`,`limited_sells`,`limited_stack`,`offer_active`,`extradata`,`badge`,`offer_id`)
SELECT @townhouse,'111008','Bookshelf Block',0,0,0,1,0,0,'1','','',-1
    FROM DUAL WHERE @townhouse IS NOT NULL;

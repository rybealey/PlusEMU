-- PixelRP: Black Mannequin, sold beside the Mannequin (boutique_mannequin1).
--
-- The boutique Mannequin with its body in #1F1D1D instead of white (Twist,
-- 2026-10-30). Only the body: an outfit put on it is drawn over it as on the
-- white one. Its own furni rather than a second look, because a mannequin's
-- click is already Wear.
--
-- The rendering half is the .nitro bundle, the icon and the FurnitureData /
-- ExternalTexts entries under nitro/overrides/, drawn by
-- docker/nitro/make-black-mannequin.py, plus the client: the body colour is
-- 99997 in renderer-config's avatar.default.figuredata, picked by the
-- renderer's mannequin patch for this classname. The rows below are inert
-- without them.
--
-- As the Mannequin: interaction 'mannequin', stack height 2, nothing stacks
-- on it. Free, like the rest of the catalog since 90_FreeCatalog.
--
-- The page is whichever sells the Mannequin.
--
-- Idempotent: a fixed furniture id, cleared before insert.

SET @page := (SELECT ci.`page_id` FROM `catalog_items` ci
    JOIN `furniture` f ON CAST(f.`id` AS CHAR) = ci.`item_id`
    WHERE f.`item_name` = 'boutique_mannequin1' ORDER BY ci.`id` LIMIT 1);

DELETE FROM `catalog_items` WHERE `item_id` = '111009';
DELETE FROM `furniture` WHERE `id` = 111009;

-- id == sprite_id, the convention every custom line here follows.
INSERT INTO `furniture` (`id`,`item_name`,`public_name`,`type`,`width`,`length`,`stack_height`,
    `can_stack`,`can_sit`,`is_walkable`,`sprite_id`,`allow_recycle`,`allow_trade`,
    `allow_marketplace_sell`,`allow_gift`,`allow_inventory_stack`,`interaction_type`,
    `behaviour_data`,`interaction_modes_count`,`is_rare`) VALUES
    (111009,'boutique_mannequin1_black','Black Mannequin','s',1,1,2,'0','0','0',111009,'0','1','1','1','1',
     'mannequin',0,1,'0');

-- Guarded on the page resolving: a DB without it gets the furni and no offer,
-- rather than an offer on page NULL.
INSERT INTO `catalog_items` (`page_id`,`item_id`,`catalog_name`,`cost_credits`,`cost_pixels`,
    `cost_diamonds`,`amount`,`limited_sells`,`limited_stack`,`offer_active`,`extradata`,`badge`,`offer_id`)
SELECT @page,'111009','Black Mannequin',0,0,0,1,0,0,'1','','',-1
    FROM DUAL WHERE @page IS NOT NULL;

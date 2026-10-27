-- PixelRP: Game Night, in Builders > Customs > Sports & Games > Board Game.
--
-- A 1x1 table-top piece drawn after pj_c26_boardgames but lower, with chess,
-- Connect 4 and UNO. Four states, switched by clicking:
--   0  low stack: chess box, Connect 4 box, UNO deck on top     0.39 high
--   1  chess and Connect 4 stacked, UNO deck on the floor       0.28
--   2  side by side, nothing stacked                            0.11
--   3  Connect 4 standing up, chess board with pieces, UNO cards 0.34
-- height_adjustable carries those heights, so a thing stacked on top sits on
-- the state it was put on; stack_height is the tallest.
--
-- The rendering half is the .nitro bundle, the icon and the FurnitureData /
-- ExternalTexts entries under nitro/overrides/, drawn by
-- docker/nitro/make-game-night.py; the rows below are inert without them.
--
-- The Board Game page is found by its path, Builders > Customs > Sports & Games
-- > Board Game, or failing that whichever page sells the Coepoly dice
-- (coepoly_dice) - both are 965612 on beta.
--
-- Trade, gift and Marketplace flags as Bedtime Board Games. Free, like the rest
-- of the catalog since 90_FreeCatalog.
--
-- Idempotent: a fixed furniture id, cleared before insert.

SET @builders := (SELECT `id` FROM `catalog_pages` WHERE `parent_id` = -1 AND `caption` = 'Builders' LIMIT 1);
SET @customs := (SELECT `id` FROM `catalog_pages` WHERE `parent_id` = @builders AND `caption` = 'Customs'
    ORDER BY `id` LIMIT 1);
SET @sports := (SELECT `id` FROM `catalog_pages` WHERE `parent_id` = @customs AND `caption` = 'Sports & Games'
    ORDER BY `id` LIMIT 1);
SET @board_game := COALESCE(
    (SELECT `id` FROM `catalog_pages` WHERE `parent_id` = @sports AND `caption` = 'Board Game'
        ORDER BY `id` LIMIT 1),
    (SELECT ci.`page_id` FROM `catalog_items` ci
        JOIN `furniture` f ON CAST(f.`id` AS CHAR) = ci.`item_id`
        WHERE f.`item_name` = 'coepoly_dice' ORDER BY ci.`id` LIMIT 1));

DELETE FROM `catalog_items` WHERE `item_id` = '111002';
DELETE FROM `furniture` WHERE `id` = 111002;

-- id == sprite_id, the convention every custom line here follows.
INSERT INTO `furniture` (`id`,`item_name`,`public_name`,`type`,`width`,`length`,`stack_height`,
    `can_stack`,`can_sit`,`is_walkable`,`sprite_id`,`allow_recycle`,`allow_trade`,
    `allow_marketplace_sell`,`allow_gift`,`allow_inventory_stack`,`interaction_type`,
    `behaviour_data`,`interaction_modes_count`,`height_adjustable`,`is_rare`) VALUES
    (111002,'game_night_boardgames','Game Night','s',1,1,0.39,'1','0','0',111002,'1','1','1','1','1',
     'default',0,4,'0.39,0.28,0.11,0.34','0');

-- Guarded on the page resolving: a DB without it gets the furni and no offer,
-- rather than an offer on page NULL.
INSERT INTO `catalog_items` (`page_id`,`item_id`,`catalog_name`,`cost_credits`,`cost_pixels`,
    `cost_diamonds`,`amount`,`limited_sells`,`limited_stack`,`offer_active`,`extradata`,`badge`,`offer_id`)
SELECT @board_game,'111002','Game Night',0,0,0,1,0,0,'1','','',-1
    FROM DUAL WHERE @board_game IS NOT NULL;

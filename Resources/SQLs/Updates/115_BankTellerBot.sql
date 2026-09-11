-- PixelRP: the Bank Teller bot - a clone of bot_generic, to build the bank on.
--
-- A catalog bot is THREE rows, not one, and missing any of them fails
-- differently:
--   * `furniture`             - the thing that is bought and sits in an
--                               inventory. Product type 'r', like every bot.
--   * `catalog_bot_presets`   - keyed on the FURNITURE id, and the only place
--                               the figure, name, motto and AI live. Without
--                               it the catalog draws a blank wall item, because
--                               CatalogPageComposer detects a bot by asking
--                               whether a preset exists at all.
--   * `catalog_items`         - what puts it in a shop page.
--
-- The FIGURE is Robbie's, verbatim. This is a template - the brief is that it
-- gets dressed into a teller - and every part id in that string is known to
-- render. Inventing a suit out of part numbers nobody can check here would
-- ship a bot with missing limbs, and a bot can be re-dressed in game.
--
-- It sits on the Bots page beside the bot it was cloned from rather than in
-- Builders > Corporations with the ATM. That is where a player looks for a
-- bot, and where the one being copied already is; moving it later is a page
-- id, nothing else.
--
-- 107500 continues PixelRP's own furni block, which ends at 106754 (the ATM),
-- with room left between them.
--
-- Free, like the rest of the catalog since 90_FreeCatalog.
--
-- Idempotent: fixed id, cleared before insert.

DELETE FROM `catalog_items` WHERE `item_id` = '107500';
DELETE FROM `catalog_bot_presets` WHERE `id` = 107500;
DELETE FROM `furniture` WHERE `id` = 107500;

-- Every flag copied from bot_generic (3569), including sprite_id 0: a bot has
-- no sprite, it is drawn from the figure above.
INSERT INTO `furniture`
    (`id`,`item_name`,`public_name`,`type`,`width`,`length`,`stack_height`,`can_stack`,`can_sit`,
     `is_walkable`,`sprite_id`,`allow_recycle`,`allow_trade`,`allow_marketplace_sell`,`allow_gift`,
     `allow_inventory_stack`,`interaction_type`,`behaviour_data`,`interaction_modes_count`,`is_rare`)
VALUES
    (107500,'bot_banker','Bank Teller','r',1,1,0.0,'0','0','0',0,'1','1','1','0','1','default',0,1,'0');

INSERT INTO `catalog_bot_presets` (`id`,`name`,`figure`,`gender`,`motto`,`ai_type`)
VALUES
    (107500,'Teller','hr-3020-34.hd-3091-2.ch-225-92.lg-3058-100.sh-3089-1338.ca-3084-78-108.wa-2005',
     'M','How can I help you today?','generic');

-- Wherever bot_generic actually sits, rather than a page id typed in here -
-- cloning a shop item means landing beside the thing it was cloned from. 9 is
-- the seeded Bots page and the fallback if the original has been removed.
SET @botpage := (SELECT `page_id` FROM `catalog_items` WHERE `item_id` = '3569' LIMIT 1);

INSERT INTO `catalog_items`
    (`page_id`,`item_id`,`catalog_name`,`cost_credits`,`cost_pixels`,`cost_diamonds`,
     `amount`,`limited_sells`,`limited_stack`,`offer_active`,`extradata`,`badge`,`offer_id`)
VALUES
    (COALESCE(@botpage, 9), '107500', 'Bank Teller', 0, 0, 0, 1, 0, 0, '1', '', '', -1);

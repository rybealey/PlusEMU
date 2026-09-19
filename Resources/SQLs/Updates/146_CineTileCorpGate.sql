-- pixelrp: the halo tile becomes a corporation gate and moves to the shelf
-- somebody building a corporation actually looks at.
--
-- Two separate things, deliberately in one file because neither is much use
-- without the other:
--
--   1. The catalog row leaves Furni > Themes > Habbowood > Cinema, where the
--      default catalog filed it because of its classname, and joins Builders >
--      Corporations. Nobody dressing a film set wants an invisible tile; the
--      person who wants one is building a staff door.
--
--   2. `cine_tile` goes from 'pressure_pad' to 'corp_gate' - the same tile,
--      lighting the same way, with a condition on who may step onto it. 138
--      made it an invisible pressure plate and that is exactly the right body
--      for this: you cannot see the gate, you only find out it will not let
--      you past.
--
-- The condition is DUTY, not employer. Anyone clocked in walks across whoever
-- they work for; anyone off duty cannot step on at all. That makes one tile
-- work for every corporation in the hotel instead of needing one per company,
-- and it is why this is not built on the guild gate - a guild gate asks which
-- group you are in, and the answer here is "any, as long as you are working".
--
-- The behaviour is inert without the emulator side (InteractionType.CorpGate,
-- resolved once per walker into the movement engine's traverse context), so
-- this file and that build ship together.
--
-- Idempotent: absolute assignments and a move by classname, so a re-run sets
-- the same values and moves a row to where it already is.

SET @builders := (SELECT `id` FROM `catalog_pages` WHERE `parent_id` = -1 AND `caption` = 'Builders' LIMIT 1);
SET @corps    := (SELECT `id` FROM `catalog_pages` WHERE `parent_id` = @builders AND `caption` = 'Corporations' LIMIT 1);

-- Matched on the classname through `furniture`, not on the id 89929 that 83
-- happens to use: the id is whatever the library assigned, the classname is
-- what the piece IS.
UPDATE `catalog_items` `ci`
    JOIN `furniture` `f` ON `f`.`id` = `ci`.`item_id`
    SET `ci`.`page_id` = @corps
    WHERE @corps IS NOT NULL
      AND `f`.`item_name` = 'cine_tile';

-- If it had no catalog row at all, "move it to Corporations" and "it is not
-- for sale anywhere" should not end in the same silence. Free, like the rest
-- of the catalog since 90.
INSERT INTO `catalog_items`
    (`page_id`,`item_id`,`catalog_name`,`cost_credits`,`cost_pixels`,`cost_diamonds`,`amount`,
     `limited_sells`,`limited_stack`,`offer_active`,`extradata`,`badge`,`offer_id`)
SELECT @corps, `f`.`id`, 'Corporation Gate', 0, 0, 0, 1, 0, 0, '1', '', '', -1
    FROM `furniture` `f`
    WHERE @corps IS NOT NULL
      AND `f`.`item_name` = 'cine_tile'
      AND NOT EXISTS (SELECT 1 FROM `catalog_items` `x` WHERE `x`.`item_id` = `f`.`id`);

-- Says what it is now. "Tile With a Halo" described the art; this describes
-- the job, which is what somebody searching the catalog will type.
UPDATE `catalog_items` `ci`
    JOIN `furniture` `f` ON `f`.`id` = `ci`.`item_id`
    SET `ci`.`catalog_name` = 'Corporation Gate'
    WHERE `f`.`item_name` = 'cine_tile';

UPDATE `furniture`
    SET `public_name` = 'Corporation Gate',
        `interaction_type` = 'corp_gate',
        `interaction_modes_count` = GREATEST(`interaction_modes_count`, 2),
        `stack_height` = 0
    WHERE `item_name` = 'cine_tile';

-- Should read corp_gate / 2 / 0, on the Corporations page.
SELECT `f`.`item_name`, `f`.`public_name`, `f`.`interaction_type`,
       `f`.`interaction_modes_count`, `f`.`stack_height`, `p`.`caption`
    FROM `furniture` `f`
    LEFT JOIN `catalog_items` `ci` ON `ci`.`item_id` = `f`.`id`
    LEFT JOIN `catalog_pages` `p` ON `p`.`id` = `ci`.`page_id`
    WHERE `f`.`item_name` = 'cine_tile';

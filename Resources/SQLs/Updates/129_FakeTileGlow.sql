-- pixelrp: the Duck Effect tile lights up while somebody is standing on it.
--
-- effect_faketile is already a furniture_multistate piece whose bundle draws
-- two states, 0 and 1 - the second is the lit one. Nothing was ever switching
-- between them, so it sat dark forever.
--
-- pressure_pad is the interaction type that now means exactly this: lit while
-- occupied, dark when empty. It was previously a stub in Item.ProcessUpdates
-- that set the state to "1" and never cleared it, and which nothing triggered;
-- the state now belongs to UserWalksOnFurni / UserWalksOffFurni, which can see
-- whether anybody is actually there.
--
-- NOTE, because this is wider than one tile: eleven furni in the base library
-- already carry pressure_pad - the dance tiles (hs_dnctile_*), wf_colortile,
-- hween12_coffin, dino_c15_volcano and others. They have been inert; from this
-- change they light up when stood on, which is what a pressure pad is for. If
-- that is unwanted for any of them, give this tile its own interaction type
-- instead and leave theirs alone.

-- The modes count tells the client the piece has a second state at all.
-- GREATEST rather than a flat 2, so a piece that already declares more keeps
-- what it has and a re-run changes nothing.
UPDATE `furniture`
    SET `interaction_type` = 'pressure_pad',
        `interaction_modes_count` = GREATEST(`interaction_modes_count`, 2)
    WHERE `item_name` = 'effect_faketile';

-- Should read pressure_pad, with interaction_modes_count 2 so the client knows
-- the piece has a second state to draw.
SELECT `item_name`, `interaction_type`, `interaction_modes_count`
    FROM `furniture` WHERE `item_name` = 'effect_faketile';

-- Everything now riding on this behaviour, for the record.
SELECT `item_name`, `public_name` FROM `furniture`
    WHERE `interaction_type` = 'pressure_pad' ORDER BY `item_name`;

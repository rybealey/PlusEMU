-- pixelrp: the halo tile becomes an invisible pressure plate.
--
-- `cine_tile` ("Tile With a Halo") ships with interaction_type 'pressure_tile',
-- which this emulator does not map - InteractionTypes has a case for
-- 'pressure_pad' and none for 'pressure_tile', so it fell through to default
-- and the furni had no walk-on behaviour at all. It has always had the ART for
-- one: two layers, a lit plate and a six-frame additive halo.
--
-- Paired with an asset override that removes the OFF-state plate frame from the
-- bundle (nitro/overrides/bundled/furniture/cine_tile.nitro), so the tile is
-- invisible until somebody stands on it and then lights up. That override uses
-- the bundle's own existing trick: the glow layer's frame 0 is declared with no
-- image, which is how it stays dark when off. The plate's off frame now does
-- the same.
--
-- stack_height goes to 0 with it. The plate was 0.14 high, which is right for a
-- tile you can see and wrong for one you cannot - an avatar standing 0.14 above
-- the floor on nothing visible reads as floating. Say so here rather than leave
-- somebody hunting for it later.
--
-- effect_faketile is deliberately NOT touched. 129 made it a pressure pad on
-- the assumption its two animation states had two different pictures; they do
-- not - state 1 is state 0 mirrored, so it can never visibly light up. It stays
-- as it is, doing nothing visible, until it is either given real art or taken
-- back off pressure_pad.
--
-- Idempotent: absolute assignments, so a re-run sets the same values.

UPDATE `furniture`
    SET `interaction_type` = 'pressure_pad',
        `interaction_modes_count` = GREATEST(`interaction_modes_count`, 2),
        `stack_height` = 0
    WHERE `item_name` = 'cine_tile';

-- Should read pressure_pad / 2 / 0.
SELECT `id`, `item_name`, `interaction_type`, `interaction_modes_count`, `stack_height`, `is_walkable`
    FROM `furniture`
    WHERE `item_name` = 'cine_tile';

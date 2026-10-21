-- Marble Tile 1 (habbox_haaziq_dalle1) gets two more looks: its dark grey
-- marble (look 9, state 8) in the grey of the Victorian pillar (peetpilaar1)
-- that stands on it, as state 10, and the same with the marble softer, as
-- state 11 (Twist, 2026-10-21).
--
-- The looks themselves are in the bundle under
-- nitro/overrides/bundled/furniture/habbox_haaziq_dalle1.nitro (two frames
-- and animations 10 and 11 added; states 0-9 draw exactly as before). This
-- row only lets a click go on from state 9 to them: 10 looks -> 12.
--
-- Runs once in effect: only touches the tile while it still has its 10.

UPDATE `furniture`
   SET `interaction_modes_count` = 12
 WHERE `item_name` = 'habbox_haaziq_dalle1' AND `interaction_modes_count` = 10;

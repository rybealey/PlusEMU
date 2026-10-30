-- The Diamond Podium (dia_podium) gets a third look: the same podium in dark
-- wood, the Classic Townhouse pillar's brown (belmondfolasade_pillar2), as
-- state 2 (Twist, 2026-10-30).
--
-- The look itself is in the bundle under
-- nitro/overrides/bundled/furniture/dia_podium.nitro, drawn by
-- docker/nitro/make-wood-podium.py (one frame of the podium layer and
-- animation 2 added; the glass, states 0 and 1, draws exactly as before). This
-- row only lets a click go on from the lit glass to it: 2 looks -> 3.
--
-- Runs once in effect: only touches the podium while it still has its 2.

UPDATE `furniture`
   SET `interaction_modes_count` = 3
 WHERE `item_name` = 'dia_podium' AND `interaction_modes_count` = 2;

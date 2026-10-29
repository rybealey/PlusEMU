-- The trash can (tinacodebasura_hl) gets a third look: shut, with its red lid
-- in pink #c03b51, as state 2 (Twist, 2026-10-30).
--
-- The look itself is in the bundle under
-- nitro/overrides/bundled/furniture/tinacodebasura_hl.nitro (one frame and
-- animation 2 added; states 0 and 1 draw exactly as before). This row only
-- lets a click go on from state 1 to it: 2 looks -> 3.
--
-- Runs once in effect: only touches the bin while it still has its 2.

UPDATE `furniture`
   SET `interaction_modes_count` = 3
 WHERE `item_name` = 'tinacodebasura_hl' AND `interaction_modes_count` = 2;

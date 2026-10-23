-- Thin Block (belmondfolasade_thinblock1) gets its nine looks again, each
-- standing in the middle of its tile instead of against one edge (Twist,
-- 2026-10-24, as ptrnhout4 in 308). Clicking goes on from the ninth look to
-- the centred ones.
--
-- The looks are in the bundle under
-- nitro/overrides/bundled/furniture/belmondfolasade_thinblock1.nitro (states
-- 9-17 draw states 0-8 moved to the tile's middle; 0-8 draw as before). This
-- raises its count from 9 to 18. No placed piece was saved past state 8.
--
-- Runs once in effect: only touches the furni while it still has its 9.

UPDATE `furniture`
   SET `interaction_modes_count` = 18
 WHERE `item_name` = 'belmondfolasade_thinblock1' AND `interaction_modes_count` = 9;

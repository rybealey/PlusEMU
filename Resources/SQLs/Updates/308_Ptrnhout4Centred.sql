-- Ptrnhout 4 (ptrnhout4), the bar on two posts, gets a second look: the same
-- bar in the middle of its tile instead of along its back edge (Twist,
-- 2026-10-23). Clicking it switches between the two.
--
-- The look is in the bundle under
-- nitro/overrides/bundled/furniture/ptrnhout4.nitro (state 1 = the same
-- picture 0.31 of a tile further in; state 0 draws as before). This raises
-- its count from 1 to 2.
--
-- One placed piece (room 26) was already saved at state 1, which drew the
-- same as 0 until now; it goes back to 0 so it stays where it stands.
--
-- Runs once in effect: both updates only touch the furni while it still has
-- its count of 1.

SET @hout := (SELECT `id` FROM `furniture`
               WHERE `item_name` = 'ptrnhout4' AND `interaction_modes_count` = 1 LIMIT 1);

UPDATE `items`
   SET `extra_data` = '0'
 WHERE `base_item` = @hout AND `extra_data` = '1';

UPDATE `furniture`
   SET `interaction_modes_count` = 2
 WHERE `id` = @hout;

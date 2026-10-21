-- The Washing Machine has two states, not three (Twist).
--
-- Its art draws three looks - door open with the cats moving, the same with
-- the cats still, door shut - and 289 gave it a count of 3 with click_states
-- '0,2' so a click skipped the still cats. It now really has two: the bundle
-- under nitro/overrides/bundled/furniture/hygge_c25_washer.nitro is
-- renumbered (docker/nitro/renumber-furni-states.py 0,2), so state 1 is the
-- door shut, and the count is 2 with no click_states.
--
-- Placed washers move with it: shut (2) -> 1, still cats (1) -> open (0). The
-- bundle still draws the door shut at 2 as well, so a washer saved at 2 after
-- this runs looks right and opens on the next click.
--
-- Runs once in effect: both updates only touch a washer still set up the 289
-- way (count 3, click_states '0,2').

SET @washer := (SELECT `id` FROM `furniture`
                 WHERE `item_name` = 'hygge_c25_washer' AND `interaction_modes_count` = 3
                   AND `click_states` = '0,2' LIMIT 1);

UPDATE `items`
   SET `extra_data` = CASE `extra_data` WHEN '2' THEN '1' WHEN '1' THEN '0' ELSE `extra_data` END
 WHERE `base_item` = @washer;

UPDATE `furniture`
   SET `interaction_modes_count` = 2, `click_states` = ''
 WHERE `id` = @washer;

-- PixelRP: the Black Mannequin (boutique_mannequin1_black) takes the same
-- interaction_modes_count as the Mannequin it is a copy of.
--
-- 329 gave it 1, the stock Mannequin's count in the original dump, but the
-- live Mannequin row has since been raised to 2 by hand, and the count is what
-- the room is told about a furni's use (Modes > 1 is "controllers may use it",
-- ItemsComposer). The Black Mannequin should be used exactly as the white one.
--
-- Copied from the Mannequin's row rather than written as 2, so it follows
-- whatever that row says; a database without it keeps the 1.

UPDATE `furniture` b
  JOIN `furniture` w ON w.`item_name` = 'boutique_mannequin1'
   SET b.`interaction_modes_count` = w.`interaction_modes_count`
 WHERE b.`item_name` = 'boutique_mannequin1_black';

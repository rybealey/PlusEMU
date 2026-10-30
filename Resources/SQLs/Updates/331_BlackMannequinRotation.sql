-- PixelRP: the Black Mannequin (boutique_mannequin1_black) turns, as the
-- Mannequin it is a copy of does.
--
-- Its bundle declares all eight directions, so the client asks for the next
-- one round - 0 to 1, a diagonal. RoomItemHandling snaps any odd rotation back
-- to 0 unless the furni has extra_rot, so it never moved. The live Mannequin
-- row has extra_rot 1, set by hand; 329 gave the copy the original dump's 0.
--
-- Copied from the Mannequin's row, as 330 did the click-state count.

UPDATE `furniture` b
  JOIN `furniture` w ON w.`item_name` = 'boutique_mannequin1'
   SET b.`extra_rot` = w.`extra_rot`
 WHERE b.`item_name` = 'boutique_mannequin1_black';

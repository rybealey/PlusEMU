-- PixelRP: the tile cursor's floating blue ring is off everywhere, and opting
-- in is the deliberate act.
--
-- 111 shipped the switch with the ring left ON, so the hotel kept behaving as
-- it always had and nothing changed until somebody turned a furni off. In
-- practice the ring is noise: it is a builder's readout, it appears for anyone
-- who walks past a furni whose artwork happens to name
-- `furniture_multiheight`, and almost none of those furni are ones anybody
-- stacks on deliberately.
--
-- So the default inverts. Existing rows are all set to 0 rather than left
-- alone, because "0 is the default" and "every furni is 0" have to be the same
-- statement - otherwise the hotel would be split between furni imported before
-- this and furni imported after, with no way to tell which was meant.
--
-- Still display only. The stacking height is untouched, stacking still lands
-- exactly where it did, and the emulator never reads the column.
--
-- The CLIENT is the other half of this: it suppresses the marker for every
-- furni and restores it only for definitions it has been told are opted in,
-- which arrive by the same two routes the rest of the Function record does.
-- A furni nobody has opted in is never mentioned on the wire at all.

ALTER TABLE `furniture`
    ALTER COLUMN `height_marker` SET DEFAULT 0;

UPDATE `furniture` SET `height_marker` = 0 WHERE `height_marker` <> 0;

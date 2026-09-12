-- PixelRP: the music player becomes a behaviour any furni can carry.
--
-- RoomJukeboxManager used to find its furni by classname - a hardcoded
-- "jukebox*1" in four places - so the hotel's own music player was welded to
-- one official Habbo item. A builder could not put it in a booth, a radio, a
-- wall-mounted panel or any of the custom cabinets the shop sells.
--
-- It is keyed on `interaction_type` = 'jukebox' now, which the Function Tool
-- already offers as "Jukebox", so assigning it is a two-click job rather than
-- a code change.
--
-- The interaction type already existed: InteractionTypes parsed 'jukebox' into
-- InteractionType.Jukebox and nothing in the emulator ever read it, because
-- the stock Habbo jukebox was never implemented here. So this takes over a
-- value that was parsed and then dropped, rather than inventing one.
--
-- This row is what keeps the existing jukebox working - without it the furni
-- would keep its 'default' behaviour and the music panel would report no
-- jukebox in any room.
--
-- Idempotent, and keyed on the classname so it cannot land on anything else.

UPDATE `furniture` SET `interaction_type` = 'jukebox'
WHERE `item_name` = 'jukebox*1' LIMIT 1;

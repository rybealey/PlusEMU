-- PixelRP: a height set in the builder Tools sticks to the item.
--
-- The Tools panel's height slider sends ITEM_STACK_HELPER (wire 3839) and the
-- emulator had no handler for it - the id is not in ClientPacketHeader at all,
-- and UpdateMagicTileEvent, which the constant list DOES name, has no handler
-- class either. So the slider only ever moved the sprite on the builder's own
-- screen. Nothing reached the server, nothing was stored, and the height went
-- back the moment anything made the client redraw the item: a reload, a
-- rotate, a drag.
--
-- `z` alone cannot carry this. Every auto-stacked item's `z` is just where the
-- stack put it, so re-applying `z` on a move would freeze EVERY item at the
-- height it happened to have rather than letting it re-stack on its new tile.
-- The intent has to be recorded separately from the result.
--
-- -1 means "no height was chosen, stack me normally", which is every existing
-- row and the behaviour they already have.

ALTER TABLE `items`
    ADD COLUMN `custom_height` DOUBLE NOT NULL DEFAULT -1 AFTER `z`;

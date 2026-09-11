-- PixelRP: Bush 3 is two tiles long, not one.
--
-- habbox_haaziq_buisson is a hedge whose art spans two tiles - its three
-- sprite layers step down-left along the Y axis and cover about 88px, well
-- past the 64px a single tile diamond is wide - but every declaration of its
-- footprint said 1x1. So it rendered long while the selection box and the
-- blocked square covered one tile, and avatars walked through half of it.
--
-- A footprint is declared three times and all three have to agree:
--
--   * this row, which is what the emulator blocks and paths around;
--   * FurnitureData.json xdim/ydim, which is what the client's placement ghost
--     covers;
--   * the .nitro bundle's logic.model.dimensions, the object's logical size in
--     the renderer.
--
-- The other two are in the same commit, under nitro/overrides/. Fixing only
-- this row would swap one mismatch for another: the server would block two
-- tiles while the client still believed it was one.
--
-- Idempotent, and keyed on the classname as well as the id so it cannot land
-- on a different furni if the id ever moves.

UPDATE `furniture` SET `width` = 1, `length` = 2
WHERE `id` = 101206 AND `item_name` = 'habbox_haaziq_buisson' LIMIT 1;

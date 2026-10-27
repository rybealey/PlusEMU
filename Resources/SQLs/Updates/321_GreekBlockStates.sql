-- PixelRP: the Greek Block (111006, 320_GreekBlock1x1) gets two states,
-- switched by clicking: 0 outlined (the whole outline on the block, so nothing
-- hides it), 1 seamless (blocks side by side join into one slab).
--
-- The pictures are in its .nitro bundle, drawn by
-- docker/nitro/make-greek-block-1x1.py. Placed blocks keep state 0, outlined.
--
-- Idempotent.

UPDATE `furniture` SET `interaction_modes_count` = 2 WHERE `id` = 111006;

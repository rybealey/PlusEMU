-- PixelRP: how high an avatar sits on a furni, set per furni in the Function
-- tool ("Sit height").
--
-- A sitter has always been drawn at the furni's stack height above its base,
-- which is also where anything stacked on it rests - so a chair whose seat is
-- lower than its back sat people on top of the back, and lowering the stack
-- height to fix them let furni sink into the chair. This separates the two.
--
-- -1 means "the stack height", so every furni keeps today's behaviour until
-- somebody sets one. In tiles, like stack_height.

ALTER TABLE `furniture`
    ADD COLUMN `sit_height` DOUBLE NOT NULL DEFAULT -1 AFTER `sit_rotation`;

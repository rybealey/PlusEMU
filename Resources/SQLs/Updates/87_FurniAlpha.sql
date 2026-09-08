-- pixelrp: a placed item remembers how see-through it is.
--
-- The infostand's build tools can fade an item so a builder can see what is
-- behind it. Without somewhere to keep that, the setting was a local preview
-- that died on reload and that nobody else in the room could see.
--
-- 100 is fully opaque and the default, so every existing item is unchanged.
-- The floor is enforced at 10 in the packet handler, not here: a column
-- constraint cannot explain itself, and fully invisible furni would be a way
-- to hide things from other people rather than a building aid.
ALTER TABLE `items`
    ADD COLUMN `alpha` TINYINT UNSIGNED NOT NULL DEFAULT 100 AFTER `rot`;

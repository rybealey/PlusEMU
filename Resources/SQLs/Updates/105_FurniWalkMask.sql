-- PixelRP: per-tile walkability for multi-tile furni.
--
-- `is_walkable` is all-or-nothing across a furni's whole footprint, which is
-- wrong for any shape that is not a filled rectangle. An L-shaped sofa occupies
-- a 2x2 square but only covers three of those tiles; the fourth is the inside
-- of the L, and it should be floor. Same for a fence with a gap, or an archway.
--
-- The mask is a per-tile override, stored in the FURNI'S OWN frame so it turns
-- with the item - rotate the sofa and the hole stays in the corner it belongs
-- to, rather than staying put on the map while the sofa moves around it.
--
-- Format: exactly `width` * `length` characters, '1' where that tile is
-- walkable and '0' where it follows the furni's normal blocking. The index is
--
--     b * width + a
--
-- with `a` running across the width and `b` along the length. Empty means no
-- override at all, which is every existing row and the current behaviour.
--
-- 64 characters is room for an 8x8, far past anything the catalog sells.

ALTER TABLE `furniture`
    ADD COLUMN `walk_mask` VARCHAR(64) NOT NULL DEFAULT '' AFTER `is_walkable`;

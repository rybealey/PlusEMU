-- PixelRP: open tiles for furni of any size the Function tool can set.
--
-- The mask is one character per tile of the footprint. 64 covered an 8 x 8,
-- but the tool now sets sizes up to 20 x 20 (MaximumSize in
-- RpFurniFunctionEvent), so a bigger furni could not keep any open tiles.
-- Widening keeps every stored mask as it is.

ALTER TABLE `furniture`
    MODIFY COLUMN `walk_mask` VARCHAR(400) NOT NULL DEFAULT '';

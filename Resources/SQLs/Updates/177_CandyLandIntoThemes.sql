-- pixelrp: Candy Land moves from the Furni tab into Themes.
--
-- It is the default dump's Candy Land page (930157 on beta, page_link
-- 'candycolture'), which 119 kept as a functional page and re-parented straight
-- under Furni. Found by caption under Furni - which is what tells it from
-- Themes' own 'Candyland' furniline, a different page that is already in
-- Themes and is not touched.
--
-- Themes went to the Builders tab in 174, so Candy Land becomes Builders-only
-- (rank 2) along with it; its own min_rank is left as it is.
--
-- Themes is alphabetical with 'More Themes' pinned last (119 wrote it that
-- way), so Candy Land is slotted in where it sorts - just before 'Candyland' -
-- and everything from there down moves one place. Nothing else is renumbered.
--
-- Idempotent: it is only moved while still directly under Furni.

SET @furni := (SELECT `id` FROM `catalog_pages` WHERE `parent_id` = -1 AND `page_link` = 'furni' LIMIT 1);
SET @builders := (SELECT `id` FROM `catalog_pages` WHERE `parent_id` = -1 AND `caption` = 'Builders' LIMIT 1);
SET @themes := (SELECT `id` FROM `catalog_pages`
                 WHERE `caption` = 'Themes' AND `parent_id` IN (@builders, @furni) LIMIT 1);
SET @candy := (SELECT `id` FROM `catalog_pages` WHERE `parent_id` = @furni AND `caption` = 'Candy Land' LIMIT 1);

-- Where it sorts: the first page of Themes that comes after it, 'More Themes'
-- aside. Past the end, it goes last before 'More Themes'. Compared as bytes,
-- the way 119's generator sorted, so the space in 'Candy Land' puts it ahead
-- of 'Candyland' whatever the column's collation says.
SET @slot := (SELECT MIN(`order_num`) FROM `catalog_pages`
               WHERE `parent_id` = @themes AND `caption` <> 'More Themes'
                 AND CAST(`caption` AS BINARY) > CAST('Candy Land' AS BINARY));
SET @slot := COALESCE(@slot,
    (SELECT `order_num` FROM `catalog_pages` WHERE `parent_id` = @themes AND `caption` = 'More Themes' LIMIT 1),
    (SELECT COALESCE(MAX(`order_num`), 0) + 1 FROM `catalog_pages` WHERE `parent_id` = @themes));

UPDATE `catalog_pages` SET `order_num` = `order_num` + 1
 WHERE `parent_id` = @themes AND `order_num` >= @slot AND @candy IS NOT NULL;

UPDATE `catalog_pages` SET `parent_id` = @themes, `order_num` = @slot
 WHERE `id` = @candy AND @themes IS NOT NULL;

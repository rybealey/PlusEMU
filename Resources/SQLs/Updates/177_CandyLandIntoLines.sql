-- pixelrp: Candy Land becomes one of the Lines in the Furni tab.
--
-- It is the default dump's Candy Land page (930157 on beta, page_link
-- 'candycolture'), which 119 kept as a functional page and re-parented straight
-- under Furni. Found by caption under Furni - which is what tells it from
-- Themes' 'Candyland' furniline, a different page that went to Builders with
-- Themes in 174 and is not touched.
--
-- Lines is renumbered alphabetically afterwards, as 175 left it, so Candy Land
-- lands between Bazaar and Classics. Same rank either way - both open to
-- everyone.
--
-- Idempotent: it is only moved while still directly under Furni, and sorting
-- an already sorted list changes nothing.

SET @furni := (SELECT `id` FROM `catalog_pages` WHERE `parent_id` = -1 AND `page_link` = 'furni' LIMIT 1);
SET @lines := (SELECT `id` FROM `catalog_pages` WHERE `parent_id` = @furni AND `caption` = 'Lines' LIMIT 1);
SET @candy := (SELECT `id` FROM `catalog_pages` WHERE `parent_id` = @furni AND `caption` = 'Candy Land' LIMIT 1);

UPDATE `catalog_pages` SET `parent_id` = @lines
 WHERE `id` = @candy AND @lines IS NOT NULL;

SET @n := 0;
UPDATE `catalog_pages` SET `order_num` = (@n := @n + 1)
 WHERE `parent_id` = @lines AND @lines IS NOT NULL
 ORDER BY `caption`;

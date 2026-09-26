-- pixelrp: Classics becomes one of the Lines in the Furni tab.
--
-- Both are 119's taxonomy categories under Furni; Classics is a single page of
-- furni with nothing below it, so it moves in as a line of its own. Lines is
-- alphabetical (119 wrote it that way and nothing since has touched it), so it
-- is renumbered afterwards and Classics lands between Bazaar and Coco.
--
-- Same rank either way - Lines and Classics are both open to everyone.
--
-- Idempotent: Classics is only moved while it is still directly under Furni,
-- and sorting an already sorted list changes nothing.

SET @furni := (SELECT `id` FROM `catalog_pages` WHERE `parent_id` = -1 AND `page_link` = 'furni' LIMIT 1);
SET @lines := (SELECT `id` FROM `catalog_pages` WHERE `parent_id` = @furni AND `caption` = 'Lines' LIMIT 1);
SET @classics := (SELECT `id` FROM `catalog_pages` WHERE `parent_id` = @furni AND `caption` = 'Classics' LIMIT 1);

UPDATE `catalog_pages` SET `parent_id` = @lines
 WHERE `id` = @classics AND @lines IS NOT NULL;

SET @n := 0;
UPDATE `catalog_pages` SET `order_num` = (@n := @n + 1)
 WHERE `parent_id` = @lines AND @lines IS NOT NULL
 ORDER BY `caption`;

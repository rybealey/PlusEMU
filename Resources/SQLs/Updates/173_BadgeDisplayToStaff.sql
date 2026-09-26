-- pixelrp: the Badge Display page moves from the Furni tab to the Staff tab.
--
-- It is the badge_display layout page (930033 on beta), which 119 re-parented
-- straight under Furni when the legacy "Furni By Item" shelf above it went.
-- Found by its layout rather than its id, the way 119 found it.
--
-- The Staff tab is rank 5, and a page is only shown when every page above it
-- lets the player in, so moving it is what hides it from players - its own
-- min_rank is left as it is.
--
-- It goes to the end of the Staff tab. Idempotent: once it is under Staff it
-- is left alone, so a re-run does not keep pushing it to the bottom.

SET @staff := (SELECT `id` FROM `catalog_pages` WHERE `parent_id` = -1 AND `page_link` = 'staff' LIMIT 1);
SET @badge := (SELECT `id` FROM `catalog_pages` WHERE `page_layout` = 'badge_display' ORDER BY `id` LIMIT 1);
SET @last := (SELECT COALESCE(MAX(`order_num`), 0) FROM `catalog_pages` WHERE `parent_id` = @staff);

UPDATE `catalog_pages`
   SET `parent_id` = @staff, `order_num` = @last + 1
 WHERE `id` = @badge AND @staff IS NOT NULL AND `parent_id` <> @staff;

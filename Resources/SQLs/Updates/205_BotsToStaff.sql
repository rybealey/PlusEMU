-- pixelrp: the Bots page moves from the Builders tab to the Staff tab.
--
-- It is the default catalog's Bots page (layout 'bots', id 9 in the dump),
-- which beta had parented under Builders. Found by its layout rather than its
-- id, the way 173 found Badge Display. The bots it sells come with it,
-- the Bank Teller (115) among them.
--
-- The Staff tab is rank 5 and Builders is rank 2, and a page is only shown
-- when every page above it lets the player in, so ranks 2-4 stop seeing bots.
-- Its own min_rank is left as it is. Turfs, which 83 put above Bots, stays
-- in Builders.
--
-- It goes to the end of the Staff tab. Idempotent: once it is under Staff it
-- is left alone, so a re-run does not keep pushing it to the bottom.

SET @staff := (SELECT `id` FROM `catalog_pages` WHERE `parent_id` = -1 AND `page_link` = 'staff' LIMIT 1);
SET @bots := (SELECT `id` FROM `catalog_pages` WHERE `page_layout` = 'bots' ORDER BY `id` LIMIT 1);
SET @last := (SELECT COALESCE(MAX(`order_num`), 0) FROM `catalog_pages` WHERE `parent_id` = @staff);

UPDATE `catalog_pages`
   SET `parent_id` = @staff, `order_num` = @last + 1
 WHERE `id` = @bots AND @staff IS NOT NULL AND `parent_id` <> @staff;

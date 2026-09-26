-- pixelrp: Games and Trophies move from the Furni tab to the Staff tab.
--
-- Games is 119's taxonomy category (a page per game, Battle Banzai, Freeze,
-- Football...), found by caption under Furni. Trophies is the public trophy
-- engraver (930027 on beta, layout 'trophies'), which 119 re-parented straight
-- under Furni; found by that layout under Furni, which is what tells it from
-- the Staff tab's own prize Trophies page (930059) - that one is already under
-- Staff and is not touched, so Staff now carries both.
--
-- The Staff tab is rank 5, and a page is only shown when every page above it
-- lets the player in, so moving them hides them from players. Their own
-- min_rank, and everything underneath Games, is left as it is.
--
-- Both go to the end of the Staff tab, Games first. Idempotent: once they are
-- under Staff they are no longer found under Furni, so a re-run moves nothing.

SET @furni := (SELECT `id` FROM `catalog_pages` WHERE `parent_id` = -1 AND `page_link` = 'furni' LIMIT 1);
SET @staff := (SELECT `id` FROM `catalog_pages` WHERE `parent_id` = -1 AND `page_link` = 'staff' LIMIT 1);
SET @games := (SELECT `id` FROM `catalog_pages` WHERE `parent_id` = @furni AND `caption` = 'Games' LIMIT 1);
SET @trophies := (SELECT `id` FROM `catalog_pages` WHERE `parent_id` = @furni AND `page_layout` = 'trophies' LIMIT 1);
SET @last := (SELECT COALESCE(MAX(`order_num`), 0) FROM `catalog_pages` WHERE `parent_id` = @staff);

UPDATE `catalog_pages` SET `parent_id` = @staff, `order_num` = @last + 1
 WHERE `id` = @games AND @staff IS NOT NULL;

UPDATE `catalog_pages` SET `parent_id` = @staff, `order_num` = @last + 2
 WHERE `id` = @trophies AND @staff IS NOT NULL;

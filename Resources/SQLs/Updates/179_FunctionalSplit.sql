-- pixelrp: the Furni tab's Functional category is split up.
--
--   Wired  -> the Staff tab
--   Sound  -> the Staff tab
--   Pets   -> the Pet Shop tab
--
-- All three are 119's taxonomy pages under Functional, found by caption there -
-- which is what tells this Wired from the Staff tab's own old 'Wired' page
-- (930109, page_link 'category_wired', disabled), which is not touched.
--
-- Staff is rank 5, and a page is only shown when every page above it lets the
-- player in, so Wired and Sound stop being sold to players. Pets stays open to
-- everyone, as the Pet Shop is. Their own min_rank, and everything underneath
-- them, is left as it is.
--
-- They go to the end of their tab: Wired then Sound on Staff, Pets after Pet
-- Accessories on the Pet Shop.
--
-- Functional is left with nothing on it and nothing under it (it never held
-- furni of its own), so it is hidden rather than deleted - visible = 0, the
-- one flag that takes it off the tab and back again.
--
-- Idempotent: each page is only moved while it is still under Functional.

SET @furni := (SELECT `id` FROM `catalog_pages` WHERE `parent_id` = -1 AND `page_link` = 'furni' LIMIT 1);
SET @staff := (SELECT `id` FROM `catalog_pages` WHERE `parent_id` = -1 AND `page_link` = 'staff' LIMIT 1);
SET @petshop := (SELECT `id` FROM `catalog_pages` WHERE `parent_id` = -1 AND `page_link` = 'pets_shop' LIMIT 1);
SET @functional := (SELECT `id` FROM `catalog_pages` WHERE `parent_id` = @furni AND `caption` = 'Functional' LIMIT 1);

SET @wired := (SELECT `id` FROM `catalog_pages` WHERE `parent_id` = @functional AND `caption` = 'Wired' LIMIT 1);
SET @sound := (SELECT `id` FROM `catalog_pages` WHERE `parent_id` = @functional AND `caption` = 'Sound' LIMIT 1);
SET @pets := (SELECT `id` FROM `catalog_pages` WHERE `parent_id` = @functional AND `caption` = 'Pets' LIMIT 1);

SET @staff_last := (SELECT COALESCE(MAX(`order_num`), 0) FROM `catalog_pages` WHERE `parent_id` = @staff);
SET @pets_last := (SELECT COALESCE(MAX(`order_num`), 0) FROM `catalog_pages` WHERE `parent_id` = @petshop);

UPDATE `catalog_pages` SET `parent_id` = @staff, `order_num` = @staff_last + 1
 WHERE `id` = @wired AND @staff IS NOT NULL;

UPDATE `catalog_pages` SET `parent_id` = @staff, `order_num` = @staff_last + 2
 WHERE `id` = @sound AND @staff IS NOT NULL;

UPDATE `catalog_pages` SET `parent_id` = @petshop, `order_num` = @pets_last + 1
 WHERE `id` = @pets AND @petshop IS NOT NULL;

-- Only once it really is empty: no page under it, no furni on it.
UPDATE `catalog_pages` SET `visible` = b'0'
 WHERE `id` = @functional
   AND NOT EXISTS (SELECT 1 FROM (SELECT `parent_id` FROM `catalog_pages`) c WHERE c.`parent_id` = @functional)
   AND NOT EXISTS (SELECT 1 FROM `catalog_items` WHERE `page_id` = @functional);

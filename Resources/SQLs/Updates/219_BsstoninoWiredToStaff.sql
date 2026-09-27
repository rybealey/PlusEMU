-- pixelrp: the two Wired effect boxes that came with Bsstonino move to the
-- Staff tab's Wired page.
--
-- wf_act_tiles and wf_act_tilss (Wired Effect: Collision On (And Next To)
-- Furniture Does Not Move Furniture) were in 122's download with the pack's
-- furni, so they were sold in Builders > Customs > Bsstonino, and 218 filed
-- them under Tech & Games. Every other wired box is on the Staff tab's Wired
-- page - 119's taxonomy page that 179 moved there - so these join them.
--
-- That page is found by caption under Staff, open pages only, which is what
-- tells it from the Staff tab's old disabled 'Wired' (930109), as 182 did.
-- Staff is rank 5, so ranks 2-4 stop seeing these two.
--
-- Only the two catalog rows move, and only while they are still on one of
-- Bsstonino's pages. Idempotent.

SET @staff := (SELECT `id` FROM `catalog_pages` WHERE `parent_id` = -1 AND `page_link` = 'staff' LIMIT 1);
SET @wired := (SELECT `id` FROM `catalog_pages`
                WHERE `parent_id` = @staff AND `caption` = 'Wired'
                  AND `visible` = b'1' AND `enabled` = b'1'
                ORDER BY `id` LIMIT 1);
SET @builders := (SELECT `id` FROM `catalog_pages` WHERE `parent_id` = -1 AND `caption` = 'Builders' LIMIT 1);
SET @customs := COALESCE(
    (SELECT `id` FROM `catalog_pages` WHERE `id` = 944000 AND `parent_id` = @builders LIMIT 1),
    (SELECT `id` FROM `catalog_pages` WHERE `parent_id` = @builders AND `caption` = 'Customs' ORDER BY `id` LIMIT 1));
SET @bsstonino := COALESCE(
    (SELECT `id` FROM `catalog_pages` WHERE `id` = 945000 AND `parent_id` = @customs LIMIT 1),
    (SELECT `id` FROM `catalog_pages` WHERE `parent_id` = @customs AND `caption` = 'Bsstonino' ORDER BY `id` LIMIT 1));

UPDATE `catalog_items` ci
  JOIN `furniture` f ON CAST(f.`id` AS CHAR) = ci.`item_id`
  JOIN `catalog_pages` p ON p.`id` = ci.`page_id`
   SET ci.`page_id` = @wired
 WHERE f.`item_name` IN ('wf_act_tiles', 'wf_act_tilss')
   AND (p.`id` = @bsstonino OR p.`parent_id` = @bsstonino)
   AND @wired IS NOT NULL;

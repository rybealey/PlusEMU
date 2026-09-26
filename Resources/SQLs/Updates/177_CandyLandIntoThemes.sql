-- pixelrp: Candy Land is merged into Themes' Candyland, and the page goes.
--
-- Candy Land is the default dump's page (930157 on beta, page_link
-- 'candycolture'), which 119 kept as a functional page and re-parented straight
-- under Furni. Candyland is 119's furniline page in Themes, which went to the
-- Builders tab in 174. They sell the same set: 31 of Candy Land's 32 furni are
-- on Candyland already, under proper names ('Blue Gum Drop Seat' rather than
-- 'cland_c15_jellyseat2'). The one that is not - cland15_unipoo - moves across,
-- named from its furniture row; the rest of Candy Land's rows and the page
-- itself are deleted.
--
-- Only ever the Candy Land page directly under Furni, found by caption, so
-- nothing else can be caught. Idempotent: once it is gone there is nothing to
-- find, and a furni already on Candyland is never added twice.

SET @furni := (SELECT `id` FROM `catalog_pages` WHERE `parent_id` = -1 AND `page_link` = 'furni' LIMIT 1);
SET @builders := (SELECT `id` FROM `catalog_pages` WHERE `parent_id` = -1 AND `caption` = 'Builders' LIMIT 1);
SET @themes := (SELECT `id` FROM `catalog_pages`
                 WHERE `caption` = 'Themes' AND `parent_id` IN (@builders, @furni) LIMIT 1);
SET @candyland := (SELECT `id` FROM `catalog_pages` WHERE `parent_id` = @themes AND `caption` = 'Candyland' LIMIT 1);
SET @candy := (SELECT `id` FROM `catalog_pages` WHERE `parent_id` = @furni AND `caption` = 'Candy Land' LIMIT 1);

-- 1. Anything on Candy Land that Candyland does not sell yet moves across.
UPDATE `catalog_items` ci
  LEFT JOIN `furniture` f ON f.`id` = ci.`item_id`
   SET ci.`page_id` = @candyland,
       ci.`catalog_name` = COALESCE(NULLIF(f.`public_name`, ''), ci.`catalog_name`)
 WHERE ci.`page_id` = @candy AND @candyland IS NOT NULL
   AND ci.`item_id` NOT IN (SELECT `item_id` FROM (SELECT `item_id` FROM `catalog_items` WHERE `page_id` = @candyland) x);

-- 2. What is left on Candy Land is on Candyland already. Only once Candyland
--    has been found, so the page is never deleted with nowhere for it to go.
DELETE FROM `catalog_items` WHERE `page_id` = @candy AND @candyland IS NOT NULL;
DELETE FROM `catalog_pages` WHERE `id` = @candy AND @candyland IS NOT NULL;

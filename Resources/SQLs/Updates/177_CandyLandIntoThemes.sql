-- pixelrp: Candy Land is merged into Themes' Candyland, and the page goes.
--
-- Candy Land is the default dump's page (930157 on beta, page_link
-- 'candycolture'), which 119 kept as a functional page and re-parented straight
-- under Furni. Candyland is 119's furniline page in Themes, which went to the
-- Builders tab in 174. They sell the same set, and all 32 of Candy Land's furni
-- are on Candyland already, under proper names ('Blue Gum Drop Seat' rather
-- than 'cland_c15_jellyseat2').
--
-- COMPARED BY SPRITE, NOT BY ID. 31 of them are the same furniture row. The
-- 32nd is Unicorn Praline under two rows: 7957 'cland15_unicornpoo' from the
-- base dump, on Candy Land, and 1000000674 'cland15_unipoo', the official
-- classname 31 added beside it because the names differ - both sprite 7957,
-- the same furni. Candyland has the second, so matching ids would have moved
-- the first across and sold it twice.
--
-- So anything whose sprite Candyland does not already sell moves across (none,
-- as it stands), named from its furniture row; the rest of Candy Land's rows
-- and the page itself are deleted.
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

-- 1. Anything on Candy Land whose sprite Candyland does not sell yet moves
--    across. A row with no furniture behind it has no sprite to compare and
--    stays, to be deleted with the page.
UPDATE `catalog_items` ci
  JOIN `furniture` f ON f.`id` = ci.`item_id`
   SET ci.`page_id` = @candyland,
       ci.`catalog_name` = COALESCE(NULLIF(f.`public_name`, ''), ci.`catalog_name`)
 WHERE ci.`page_id` = @candy AND @candyland IS NOT NULL
   AND f.`sprite_id` NOT IN (
       SELECT `sprite_id` FROM (
           SELECT cf.`sprite_id` FROM `catalog_items` c JOIN `furniture` cf ON cf.`id` = c.`item_id`
            WHERE c.`page_id` = @candyland) x);

-- 2. What is left on Candy Land is on Candyland already. Only once Candyland
--    has been found, so the page is never deleted with nowhere for it to go.
DELETE FROM `catalog_items` WHERE `page_id` = @candy AND @candyland IS NOT NULL;
DELETE FROM `catalog_pages` WHERE `id` = @candy AND @candyland IS NOT NULL;

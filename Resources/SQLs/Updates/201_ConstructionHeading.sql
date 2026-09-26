-- pixelrp: Builders > Construction becomes a heading over Customs and Designer.
--
-- Construction (943000, 93) has been emptied page by page - its alphabets,
-- blocks and figures went to Builders Club (188-191) - until only Tiles
-- (943070, 17 floor tiles) was left. Tiles moves into Customs, whose sets are
-- kept alphabetical (193), and Construction turns into a group label: the
-- client draws a page whose page_link is 'heading' as its caption, padded,
-- over the categories after it (CatalogNavigationItemView), so the tab reads
--
--     ...
--     CONSTRUCTION
--     Customs
--     Designer
--     -
--     Themes
--     ...
--
-- A heading is disabled (enabled = 0), as a divider is: the index sends it
-- with no page id, so it cannot be opened and search passes over it. Should
-- any other page still hang under Construction it goes to Customs with Tiles;
-- if furni sits on Construction itself it stays a page and nothing changes.
--
-- The divider 84 put above Kasja's old slot (940001) stands directly above
-- Construction; the heading's own padding sets the group apart, so it goes.
--
-- Idempotent: a re-run finds nothing under Construction and no divider.

SET @builders := (SELECT `id` FROM `catalog_pages` WHERE `parent_id` = -1 AND `caption` = 'Builders' LIMIT 1);
SET @construction := COALESCE(
    (SELECT `id` FROM `catalog_pages` WHERE `id` = 943000 AND `parent_id` = @builders LIMIT 1),
    (SELECT `id` FROM `catalog_pages` WHERE `parent_id` = @builders AND `caption` = 'Construction' ORDER BY `id` LIMIT 1));
SET @customs := COALESCE(
    (SELECT `id` FROM `catalog_pages` WHERE `id` = 944000 AND `parent_id` = @builders LIMIT 1),
    (SELECT `id` FROM `catalog_pages` WHERE `parent_id` = @builders AND `caption` = 'Customs' ORDER BY `id` LIMIT 1));

UPDATE `catalog_pages` SET `parent_id` = @customs
 WHERE `parent_id` = @construction AND @construction IS NOT NULL AND @customs IS NOT NULL;

SET @n := 0;
UPDATE `catalog_pages` SET `order_num` = (@n := @n + 1)
 WHERE `parent_id` = @customs AND @customs IS NOT NULL
 ORDER BY `caption`;

UPDATE `catalog_pages` p
   SET p.`page_link` = 'heading', p.`enabled` = b'0', p.`visible` = b'1'
 WHERE p.`id` = @construction
   AND NOT EXISTS (SELECT 1 FROM `catalog_items` ci WHERE ci.`page_id` = p.`id`)
   AND NOT EXISTS (SELECT 1 FROM (SELECT `parent_id` FROM `catalog_pages`) c WHERE c.`parent_id` = p.`id`);

DELETE FROM `catalog_pages`
 WHERE `id` = 940001 AND `page_link` = 'divider' AND `parent_id` = @builders
   AND EXISTS (SELECT 1 FROM (SELECT `id`, `page_link` FROM `catalog_pages`) c
                WHERE c.`id` = @construction AND c.`page_link` = 'heading');

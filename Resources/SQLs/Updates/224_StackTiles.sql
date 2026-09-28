-- pixelrp: the stack tiles get a category of their own, and all six work.
--
-- Builders > Stack Tiles, in the Construction group right under Customs:
--
--     CONSTRUCTION
--     Builders Club
--     Customs
--     Stack Tiles
--     HABBO
--     ...
--
-- The six stack magic tiles were sold on Themes > Extras, where 119's taxonomy
-- put Habbo's 'extras' furniline, beside the Walk Magic Tiles, the One Way
-- Gates and some seventy other furni; 174 then took Themes to the Builders tab.
-- They move to the new page, one row each, smallest first - and off any other
-- page that sells them, so each is sold once. Extras keeps the rest.
--
-- Three of them did not work. 31_FullFurniLibrary imported the 4x4, 6x6 and
-- 8x8 with interaction 'default', as it does every furni it brings in, so furni
-- put on them stacked on whatever was underneath instead of going to the height
-- the tile was set to: RoomItemHandling reads that height only off a
-- 'stacktool'. The three the original database came with always were one; now
-- all six are. Nothing else about the three differs from them - size, sprite
-- and flags are right.
--
-- The page wears the 1x1 tile's own icon with its "1x1" label painted out,
-- which is unreadable at 18x18: icon_7005103.png, 197's 7000000 + sprite
-- scheme. No other page wears it.
--
-- Idempotent: a fixed page id, the order is renumbered rather than shifted,
-- and the six rows are replaced rather than added to.

SET @builders := (SELECT `id` FROM `catalog_pages` WHERE `parent_id` = -1 AND `caption` = 'Builders' LIMIT 1);
SET @customs := COALESCE(
    (SELECT `id` FROM `catalog_pages` WHERE `id` = 944000 AND `parent_id` = @builders LIMIT 1),
    (SELECT `id` FROM `catalog_pages` WHERE `parent_id` = @builders AND `caption` = 'Customs' ORDER BY `id` LIMIT 1));
SET @customs_order := (SELECT `order_num` FROM `catalog_pages` WHERE `id` = @customs);
SET @rank := COALESCE((SELECT `min_rank` FROM `catalog_pages` WHERE `id` = @customs), 2);

UPDATE `furniture` SET `interaction_type` = 'stacktool'
 WHERE `item_name` IN ('tile_stackmagic', 'tile_stackmagic1', 'tile_stackmagic2',
                       'tile_stackmagic4x4', 'tile_stackmagic6x6', 'tile_stackmagic8x8');

INSERT IGNORE INTO `catalog_pages`
    (`id`,`parent_id`,`caption`,`icon_image`,`min_rank`,`min_vip`,`order_num`,`page_link`,
     `page_layout`,`page_strings_1`,`page_strings_2`,`visible`,`enabled`)
SELECT 959000, @builders, 'Stack Tiles', 7005103, @rank, 0, @customs_order, '', 'default_3x3', '', '', b'1', b'1'
  FROM DUAL
 WHERE @builders IS NOT NULL AND @customs_order IS NOT NULL;

-- Right after Customs; everything else keeps the order it has. ROW_NUMBER
-- keeps the derived table materialised, so MySQL lets it read the table being
-- updated (95 and 208 do the same).
UPDATE `catalog_pages` p
  JOIN (
      SELECT c.`id`,
             ROW_NUMBER() OVER (ORDER BY
                 CASE WHEN c.`id` = 959000 THEN @customs_order ELSE c.`order_num` END,
                 CASE WHEN c.`id` = @customs THEN 0 WHEN c.`id` = 959000 THEN 1 ELSE 2 END,
                 c.`id`) AS `rn`
        FROM `catalog_pages` c
       WHERE c.`parent_id` = @builders
  ) o ON o.`id` = p.`id`
   SET p.`order_num` = o.`rn`
 WHERE @customs_order IS NOT NULL;

-- The shop lists a page's rows in the order they were made, so the six are
-- taken off every page and made again here, smallest first. Only once the page
-- exists: without it they would go nowhere. Named inline rather than joined
-- from a helper table, so no string column is compared across tables.
DELETE ci FROM `catalog_items` ci
  JOIN `furniture` f ON CAST(f.`id` AS CHAR) = ci.`item_id`
 WHERE f.`item_name` IN ('tile_stackmagic', 'tile_stackmagic1', 'tile_stackmagic2',
                         'tile_stackmagic4x4', 'tile_stackmagic6x6', 'tile_stackmagic8x8')
   AND EXISTS (SELECT 1 FROM `catalog_pages` WHERE `id` = 959000);

INSERT INTO `catalog_items`
    (`page_id`,`item_id`,`catalog_name`,`cost_credits`,`cost_pixels`,`cost_diamonds`,`amount`,
     `limited_sells`,`limited_stack`,`offer_active`,`extradata`,`badge`,`offer_id`)
SELECT 959000, CAST(f.`id` AS CHAR), LEFT(COALESCE(NULLIF(f.`public_name`, ''), f.`item_name`), 100),
       0, 0, 0, 1, 0, 0, '1', '', '', -1
  FROM `furniture` f
 WHERE f.`item_name` IN ('tile_stackmagic', 'tile_stackmagic1', 'tile_stackmagic2',
                         'tile_stackmagic4x4', 'tile_stackmagic6x6', 'tile_stackmagic8x8')
   AND f.`id` = (SELECT MIN(x.`id`) FROM `furniture` x WHERE x.`item_name` = f.`item_name`)
   AND EXISTS (SELECT 1 FROM `catalog_pages` WHERE `id` = 959000)
 ORDER BY FIELD(f.`item_name`, 'tile_stackmagic', 'tile_stackmagic1', 'tile_stackmagic2',
                'tile_stackmagic4x4', 'tile_stackmagic6x6', 'tile_stackmagic8x8');

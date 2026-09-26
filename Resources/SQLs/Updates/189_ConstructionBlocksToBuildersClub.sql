-- pixelrp: Construction's material blocks go to Builders > Builders Club >
-- Materials, each to its own material - or, where Builders Club already sells
-- the same block, off the shop.
--
-- Each of Construction's 17 blocks (furniture 104326-104342) shares its
-- bundle with Habbo's official Builders Club set of that material
-- (bc_block_water*1-*6 and so on; nitro/overrides/bundled/furniture serves
-- both) and carries no partcolors - it is the untinted block. So:
--
--   * 10 materials have an official variant tinted #ffffff throughout - it
--     draws exactly like the untinted one: Brick, Metal Crate, Glass (the
--     pack's Crystal), Lava, Marble, Tile, Flower Hedge, Stone, Terra, Wool,
--     each colour 14. Where that variant is on sale on an open page, ours is a
--     duplicate and comes off the shop. Where it is not, ours moves instead.
--   * 7 have no such variant - Water, Sand, Art Deco, Grass, Industrial,
--     Metal, Wood - so ours is a colour the set lacks, and moves.
--
-- A block that moves goes to its material's page under Builders Club >
-- Materials (182's pages, by caption); if that page is not there, to Materials
-- itself; failing that, to Builders Club. The furniture rows, and every copy
-- anybody owns, are untouched. Construction > Blocks (943029), then empty,
-- goes. Idempotent; nothing moves if Builders Club is not there.

SET @builders := (SELECT `id` FROM `catalog_pages` WHERE `parent_id` = -1 AND `caption` = 'Builders' LIMIT 1);
SET @bc := (SELECT `id` FROM `catalog_pages` WHERE `parent_id` = @builders AND `caption` = 'Builders Club' ORDER BY `id` LIMIT 1);
SET @materials := (SELECT `id` FROM `catalog_pages` WHERE `parent_id` = @bc AND `caption` = 'Materials' ORDER BY `id` LIMIT 1);

DROP TABLE IF EXISTS `_cb_block`;
CREATE TABLE `_cb_block` (
    `furni_id` INT NOT NULL PRIMARY KEY, `material` VARCHAR(35) NOT NULL, `plain_sprite` INT NULL,
    `target` INT NULL) ENGINE=InnoDB DEFAULT CHARSET=latin1;
INSERT INTO `_cb_block` (`furni_id`, `material`, `plain_sprite`) VALUES
    (104326, 'Water', NULL), (104327, 'Sand', NULL), (104328, 'Art Deco', NULL),
    (104329, 'Brick', 5544), (104330, 'Metal Crate', 5671), (104331, 'Glass', 5615),
    (104332, 'Grass', NULL), (104333, 'Industrial', NULL), (104334, 'Lava', 5568),
    (104335, 'Marble', 5594), (104336, 'Metal', NULL), (104337, 'Wood', NULL),
    (104338, 'Tile', 5778), (104339, 'Flower Hedge', 5806), (104340, 'Stone', 5657),
    (104341, 'Terra', 5792), (104342, 'Wool', 5643);

UPDATE `_cb_block` b
  LEFT JOIN `catalog_pages` m ON m.`parent_id` = @materials AND m.`caption` = b.`material`
   SET b.`target` = COALESCE(m.`id`, @materials, @bc);

-- the duplicates: the plain white variant is on sale on an open page
DELETE ci FROM `catalog_items` ci
  JOIN `_cb_block` b ON CAST(b.`furni_id` AS CHAR) = ci.`item_id`
 WHERE ci.`page_id` BETWEEN 943029 AND 943046 AND @bc IS NOT NULL
   AND b.`plain_sprite` IS NOT NULL
   AND EXISTS (SELECT 1 FROM (SELECT c2.`page_id`, f2.`sprite_id`
                                FROM `catalog_items` c2
                                JOIN `furniture` f2 ON CAST(f2.`id` AS CHAR) = c2.`item_id` AND f2.`type` = 's'
                                JOIN `catalog_pages` p2 ON p2.`id` = c2.`page_id` AND p2.`visible` = b'1' AND p2.`enabled` = b'1') x
                WHERE x.`sprite_id` = b.`plain_sprite`);

-- the rest move to their material
UPDATE `catalog_items` ci
  JOIN `_cb_block` b ON CAST(b.`furni_id` AS CHAR) = ci.`item_id`
   SET ci.`page_id` = b.`target`
 WHERE ci.`page_id` BETWEEN 943029 AND 943046 AND b.`target` IS NOT NULL;

DROP TABLE `_cb_block`;

DELETE p FROM `catalog_pages` p
 WHERE p.`id` = 943029 AND @bc IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM `catalog_items` ci WHERE ci.`page_id` = p.`id`)
   AND NOT EXISTS (SELECT 1 FROM (SELECT `parent_id` FROM `catalog_pages`) c WHERE c.`parent_id` = p.`id`);

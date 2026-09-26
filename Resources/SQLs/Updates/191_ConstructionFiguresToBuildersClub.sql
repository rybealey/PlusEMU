-- pixelrp: Construction > Figures goes to Builders > Builders Club > Shapes -
-- or, where Builders Club already sells the same piece, off the shop.
--
-- Every figure in the pack is a Builders Club shape:
--
--   * 59 Quarter Circles (bc_quartercircle_color_NN) are Habbo's own
--     quarter-circle artwork with ONE of the official colours fixed in its
--     visualization - _02 is #ffd837, the official bc_quartercircle*2; _01
--     bakes *1's beige into its pixels. Each is that official colour.
--   * 68 Staircases (escalierN_yvess, 1-69) are the same stairs in the same
--     numbered colours as bc_stairs*N (60 measured exactly, the rest within
--     the shading of the measurement). Staircase 70, a black the official
--     palette does not have, is the exception.
--   * The 19 single shapes share their bundle with the official 69-colour
--     sets and carry no tint: the untinted colour, which the official sets
--     do not have - except the Small Block, which is bc_block_small*14.
--
-- So a piece whose official twin is on sale on an open page is a duplicate
-- and comes off the shop (128 have a twin); every other piece moves to its
-- shape's page under Builders Club > Shapes (182's pages, by caption) -
-- failing that to Shapes, failing that to Builders Club. The furniture rows,
-- and every copy anybody owns, are untouched. Figures' pages (943048-943069)
-- go once empty. Idempotent; nothing moves if Builders Club is not there.

SET @builders := (SELECT `id` FROM `catalog_pages` WHERE `parent_id` = -1 AND `caption` = 'Builders' LIMIT 1);
SET @bc := (SELECT `id` FROM `catalog_pages` WHERE `parent_id` = @builders AND `caption` = 'Builders Club' ORDER BY `id` LIMIT 1);
SET @shapes := (SELECT `id` FROM `catalog_pages` WHERE `parent_id` = @bc AND `caption` = 'Shapes' ORDER BY `id` LIMIT 1);

DROP TABLE IF EXISTS `_cf_piece`;
CREATE TABLE `_cf_piece` (
    `furni_id` INT NOT NULL PRIMARY KEY, `shape` VARCHAR(35) NOT NULL, `twin_sprite` INT NULL,
    `target` INT NULL) ENGINE=InnoDB DEFAULT CHARSET=latin1;
INSERT INTO `_cf_piece` (`furni_id`, `shape`, `twin_sprite`) VALUES
    (104365, 'Panels', NULL),
    (104366, 'Blocks', NULL),
    (104367, 'Small Blocks', 12290),
    (104368, 'Cones', NULL),
    (104369, 'Wedges', NULL),
    (104370, 'Curved Ramps', NULL),
    (104371, 'Cylinders', NULL),
    (104372, 'Quarter Rings', NULL),
    (104373, 'Glass Panels', NULL),
    (104374, 'Hemispheres', NULL),
    (104375, 'Half Cylinders', NULL),
    (104376, 'Blocks', NULL),
    (104377, 'Pyramids', NULL),
    (104378, 'Quarter Circles', 12774),
    (104379, 'Quarter Circles', 12785),
    (104380, 'Quarter Circles', 12796),
    (104381, 'Quarter Circles', 12807),
    (104382, 'Quarter Circles', 12818),
    (104383, 'Quarter Circles', 12829),
    (104384, 'Quarter Circles', 12840),
    (104385, 'Quarter Circles', 12841),
    (104386, 'Quarter Circles', 12842),
    (104387, 'Quarter Circles', 12775),
    (104388, 'Quarter Circles', 12776),
    (104389, 'Quarter Circles', 12777),
    (104390, 'Quarter Circles', 12778),
    (104391, 'Quarter Circles', 12779),
    (104392, 'Quarter Circles', 12780),
    (104393, 'Quarter Circles', 12781),
    (104394, 'Quarter Circles', 12782),
    (104395, 'Quarter Circles', 12783),
    (104396, 'Quarter Circles', 12784),
    (104397, 'Quarter Circles', 12786),
    (104398, 'Quarter Circles', 12787),
    (104399, 'Quarter Circles', 12788),
    (104400, 'Quarter Circles', 12789),
    (104401, 'Quarter Circles', 12790),
    (104402, 'Quarter Circles', 12791),
    (104403, 'Quarter Circles', 12792),
    (104404, 'Quarter Circles', 12793),
    (104405, 'Quarter Circles', 12794),
    (104406, 'Quarter Circles', 12795),
    (104407, 'Quarter Circles', 12797),
    (104408, 'Quarter Circles', 12798),
    (104409, 'Quarter Circles', 12799),
    (104410, 'Quarter Circles', 12800),
    (104411, 'Quarter Circles', 12801),
    (104412, 'Quarter Circles', 12802),
    (104413, 'Quarter Circles', 12803),
    (104414, 'Quarter Circles', 12804),
    (104415, 'Quarter Circles', 12805),
    (104416, 'Quarter Circles', 12806),
    (104417, 'Quarter Circles', 12808),
    (104418, 'Quarter Circles', 12809),
    (104419, 'Quarter Circles', 12810),
    (104420, 'Quarter Circles', 12811),
    (104421, 'Quarter Circles', 12812),
    (104422, 'Quarter Circles', 12813),
    (104423, 'Quarter Circles', 12814),
    (104424, 'Quarter Circles', 12815),
    (104425, 'Quarter Circles', 12816),
    (104426, 'Quarter Circles', 12817),
    (104427, 'Quarter Circles', 12819),
    (104428, 'Quarter Circles', 12831),
    (104429, 'Quarter Circles', 12832),
    (104430, 'Quarter Circles', 12833),
    (104431, 'Quarter Circles', 12834),
    (104432, 'Quarter Circles', 12835),
    (104433, 'Quarter Circles', 12836),
    (104434, 'Quarter Circles', 12837),
    (104435, 'Quarter Circles', 12838),
    (104436, 'Quarter Circles', 12839),
    (104437, 'Quarter Circles', NULL),
    (104438, 'Ramps', NULL),
    (104439, 'Rounds', NULL),
    (104440, 'Spheres', NULL),
    (104441, 'Stairs', 12637),
    (104442, 'Stairs', 12638),
    (104443, 'Stairs', 12639),
    (104444, 'Stairs', 12640),
    (104445, 'Stairs', 12641),
    (104446, 'Stairs', 12642),
    (104447, 'Stairs', 12643),
    (104448, 'Stairs', 12644),
    (104449, 'Stairs', 12645),
    (104450, 'Stairs', 12646),
    (104451, 'Stairs', 12636),
    (104452, 'Stairs', 12648),
    (104453, 'Stairs', 12649),
    (104454, 'Stairs', 12650),
    (104455, 'Stairs', 12651),
    (104456, 'Stairs', 12652),
    (104457, 'Stairs', 12653),
    (104458, 'Stairs', 12654),
    (104459, 'Stairs', 12655),
    (104460, 'Stairs', 12656),
    (104461, 'Stairs', 12657),
    (104462, 'Stairs', 12647),
    (104463, 'Stairs', 12659),
    (104464, 'Stairs', 12660),
    (104465, 'Stairs', 12662),
    (104466, 'Stairs', 12663),
    (104467, 'Stairs', 12664),
    (104468, 'Stairs', 12665),
    (104469, 'Stairs', 12666),
    (104470, 'Stairs', 12667),
    (104471, 'Stairs', 12668),
    (104472, 'Stairs', 12658),
    (104473, 'Stairs', 12670),
    (104474, 'Stairs', 12671),
    (104475, 'Stairs', 12672),
    (104476, 'Stairs', 12673),
    (104477, 'Stairs', 12674),
    (104478, 'Stairs', 12675),
    (104479, 'Stairs', 12676),
    (104480, 'Stairs', 12677),
    (104481, 'Stairs', 12678),
    (104482, 'Stairs', 12679),
    (104483, 'Stairs', 12669),
    (104484, 'Stairs', 12681),
    (104485, 'Stairs', 12682),
    (104486, 'Stairs', 12683),
    (104487, 'Stairs', 12684),
    (104488, 'Stairs', 12685),
    (104489, 'Stairs', 12686),
    (104490, 'Stairs', 12687),
    (104491, 'Stairs', 12688),
    (104492, 'Stairs', 12689),
    (104493, 'Stairs', 12690),
    (104494, 'Stairs', 12680),
    (104495, 'Stairs', 12692),
    (104496, 'Stairs', 12693),
    (104497, 'Stairs', 12694),
    (104498, 'Stairs', 12695),
    (104499, 'Stairs', 12696),
    (104500, 'Stairs', 12697),
    (104501, 'Stairs', 12698),
    (104502, 'Stairs', 12699),
    (104503, 'Stairs', 12700),
    (104504, 'Stairs', 12701),
    (104505, 'Stairs', 12691),
    (104506, 'Stairs', NULL),
    (104507, 'Stairs', 12702),
    (104508, 'Stairs', 12703),
    (104509, 'Stairs', 12704),
    (104510, 'Prisms', NULL),
    (104511, 'Standing Half Cylinders', NULL),
    (104512, 'Standing Prisms', NULL);

UPDATE `_cf_piece` c
  LEFT JOIN `catalog_pages` s ON s.`parent_id` = @shapes AND s.`caption` = c.`shape`
   SET c.`target` = COALESCE(s.`id`, @shapes, @bc);

-- every sprite on sale on an open page, once, for the twin test
DROP TABLE IF EXISTS `_cf_sold`;
CREATE TABLE `_cf_sold` (`sprite_id` INT NOT NULL PRIMARY KEY) ENGINE=InnoDB;
INSERT IGNORE INTO `_cf_sold` (`sprite_id`)
    SELECT f.`sprite_id`
      FROM `catalog_items` ci
      JOIN `furniture` f ON CAST(f.`id` AS CHAR) = ci.`item_id` AND f.`type` = 's'
      JOIN `catalog_pages` p ON p.`id` = ci.`page_id` AND p.`visible` = b'1' AND p.`enabled` = b'1'
     WHERE ci.`page_id` NOT BETWEEN 943048 AND 943069;

-- the duplicates
DELETE ci FROM `catalog_items` ci
  JOIN `_cf_piece` c ON CAST(c.`furni_id` AS CHAR) = ci.`item_id`
  JOIN `_cf_sold` s ON s.`sprite_id` = c.`twin_sprite`
 WHERE ci.`page_id` BETWEEN 943048 AND 943069 AND @bc IS NOT NULL;

-- the rest move to their shape
UPDATE `catalog_items` ci
  JOIN `_cf_piece` c ON CAST(c.`furni_id` AS CHAR) = ci.`item_id`
   SET ci.`page_id` = c.`target`
 WHERE ci.`page_id` BETWEEN 943048 AND 943069 AND c.`target` IS NOT NULL;

DROP TABLE `_cf_sold`;
DROP TABLE `_cf_piece`;

-- Figures' pages once empty: the shape pages, then Figures.
DELETE p FROM `catalog_pages` p
 WHERE p.`id` BETWEEN 943049 AND 943069 AND p.`parent_id` = 943048 AND @bc IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM `catalog_items` ci WHERE ci.`page_id` = p.`id`)
   AND NOT EXISTS (SELECT 1 FROM (SELECT `parent_id` FROM `catalog_pages`) c WHERE c.`parent_id` = p.`id`);
DELETE p FROM `catalog_pages` p
 WHERE p.`id` = 943048 AND @bc IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM `catalog_items` ci WHERE ci.`page_id` = p.`id`)
   AND NOT EXISTS (SELECT 1 FROM (SELECT `parent_id` FROM `catalog_pages`) c WHERE c.`parent_id` = p.`id`);

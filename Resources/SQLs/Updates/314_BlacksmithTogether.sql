-- pixelrp: the blacksmith set on one page - Builders > Customs > Fantasy &
-- Medieval > Blacksmith.
--
-- Blacksmith sells Habbox's forge set (habbox_shone_forgeron_*, 14 pieces,
-- most of them clicking through several colours). The same art came in again
-- as single-colour pieces under other classnames - co_rinn*, co_inna_sc2,
-- corinna_* and *_new_corinna - and those were spread over four pages away
-- from it: the barrels, armour stands, bucket, sack and trophy on Armoury,
-- the shields, banners and drawer chests on Dark Cabinets, four cupboards on
-- Storage 1 and the workbench on Tables and Desks 1. All 26 now sell on
-- Blacksmith, after the set's own 14.
--
-- A page they leave that is now empty (Armoury and Dark Cabinets) is hidden,
-- not deleted - a hidden page is one flag to undo. The three Alaska Katte
-- pieces some downloads file with this set stay on Chalet with the other 19
-- of theirs. Nothing is created; the furniture rows, and every copy anybody
-- owns, are untouched.
--
-- Idempotent: a second run finds every piece already on Blacksmith, moves
-- nothing and hides nothing.

-- Blacksmith by its id, else by what it sells. Not by caption: Customs has
-- two pages called Fantasy & Medieval (an empty hidden 955009 and the real
-- 965157), and the first by id is the wrong one.
SET @blacksmith := COALESCE(
    (SELECT `id` FROM `catalog_pages` WHERE `id` = 955285 AND `caption` = 'Blacksmith' LIMIT 1),
    (SELECT ci.`page_id` FROM `catalog_items` ci
       JOIN `furniture` f ON CAST(f.`id` AS CHAR) = ci.`item_id`
      WHERE f.`item_name` = 'habbox_shone_forgeron_armoire' ORDER BY ci.`id` LIMIT 1));

-- A temporary table, so the whole file can be tried inside a transaction and
-- rolled back (CREATE TABLE would commit it).
DROP TEMPORARY TABLE IF EXISTS `_bsm_piece`;
CREATE TEMPORARY TABLE `_bsm_piece` (`item_name` VARCHAR(70) NOT NULL PRIMARY KEY) ENGINE=InnoDB;
INSERT INTO `_bsm_piece` (`item_name`) VALUES
    ('co_inna_sc2'), ('co_rinn_as2'), ('co_rinn_as3'), ('co_rinn_as4'),
    ('co_rinn_asa'), ('co_rinn_asb'), ('co_rinn_asc'),
    ('co_rinna_1'), ('co_rinna_2'), ('co_rinna_3'),
    ('corinna_furni2'), ('corinna_furni3'), ('corinna_furni4'), ('corinna_furni5'),
    ('corinna_furni6'), ('corinna_furni9'), ('corinna_furnii'), ('corinna_furnis'),
    ('corinna_sc2'), ('corinna_sc3'), ('corinna_sc4'),
    ('furn1_new_corinna'), ('furn2_new_corinna'), ('furn3_new_corinna'),
    ('furni_new_corinna'), ('furn4_new_corinna');

-- The pages they are leaving, noted before anything moves.
DROP TEMPORARY TABLE IF EXISTS `_bsm_from`;
CREATE TEMPORARY TABLE `_bsm_from` (`page_id` INT NOT NULL PRIMARY KEY) ENGINE=InnoDB;
INSERT IGNORE INTO `_bsm_from` (`page_id`)
    SELECT ci.`page_id`
      FROM `catalog_items` ci
      JOIN `furniture` f ON CAST(f.`id` AS CHAR) = ci.`item_id`
      JOIN `_bsm_piece` p ON p.`item_name` = f.`item_name`
     WHERE ci.`page_id` <> @blacksmith AND @blacksmith IS NOT NULL;

UPDATE `catalog_items` ci
  JOIN `furniture` f ON CAST(f.`id` AS CHAR) = ci.`item_id`
  JOIN `_bsm_piece` p ON p.`item_name` = f.`item_name`
   SET ci.`page_id` = @blacksmith
 WHERE ci.`page_id` <> @blacksmith AND @blacksmith IS NOT NULL;

-- A piece that was sold on two of those pages would now be listed twice:
-- the first row stays.
DELETE ci FROM `catalog_items` ci
  JOIN `catalog_items` k ON k.`page_id` = ci.`page_id` AND k.`item_id` = ci.`item_id` AND k.`id` < ci.`id`
  JOIN `furniture` f ON CAST(f.`id` AS CHAR) = ci.`item_id`
  JOIN `_bsm_piece` p ON p.`item_name` = f.`item_name`
 WHERE ci.`page_id` = @blacksmith;

-- Hide a page left with nothing on it and nothing under it.
DELETE fr FROM `_bsm_from` fr
 WHERE EXISTS (SELECT 1 FROM `catalog_items` ci WHERE ci.`page_id` = fr.`page_id`)
    OR EXISTS (SELECT 1 FROM `catalog_pages` ch WHERE ch.`parent_id` = fr.`page_id`);
UPDATE `catalog_pages` cp
  JOIN `_bsm_from` fr ON fr.`page_id` = cp.`id`
   SET cp.`visible` = b'0', cp.`enabled` = b'0';

DROP TEMPORARY TABLE IF EXISTS `_bsm_from`;
DROP TEMPORARY TABLE IF EXISTS `_bsm_piece`;

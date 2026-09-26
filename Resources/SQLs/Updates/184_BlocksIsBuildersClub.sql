-- pixelrp: the Builders tab's Blocks category becomes Builders Club, and the
-- Builders Club 182 made moves into it.
--
-- Blocks is one of the Builders tab's own pages - it exists on the server and
-- in no migration, so it is found by caption directly under Builders. 182
-- built a second group for the Builders Club furni it took out of Misc
-- (953101, below a divider at 953100). Two categories both called Builders
-- Club would be one too many, so after the rename:
--
--   * 182's Shapes, Materials and Letters & Numbers (953102-953104) move
--     inside the renamed page, after whatever it already holds;
--   * anything sold on 953101 itself (the Builders Club Birthday trophy) moves
--     onto it too - unless the page already sells that furni, same sprite;
--   * 953101 and its divider are deleted once empty.
--
-- If there is no Blocks page under Builders, nothing here changes anything.
-- Idempotent: a second run finds no Blocks, and 953101 is gone.

SET @builders := (SELECT `id` FROM `catalog_pages` WHERE `parent_id` = -1 AND `caption` = 'Builders' LIMIT 1);
SET @blocks := (SELECT `id` FROM `catalog_pages` WHERE `parent_id` = @builders AND `caption` = 'Blocks' ORDER BY `id` LIMIT 1);
SET @last := (SELECT COALESCE(MAX(`order_num`), 0) FROM `catalog_pages` WHERE `parent_id` = @blocks);

UPDATE `catalog_pages` SET `caption` = 'Builders Club' WHERE `id` = @blocks;

UPDATE `catalog_pages` SET `parent_id` = @blocks, `order_num` = @last + 1 WHERE `id` = 953102 AND @blocks IS NOT NULL;
UPDATE `catalog_pages` SET `parent_id` = @blocks, `order_num` = @last + 2 WHERE `id` = 953103 AND @blocks IS NOT NULL;
UPDATE `catalog_pages` SET `parent_id` = @blocks, `order_num` = @last + 3 WHERE `id` = 953104 AND @blocks IS NOT NULL;

-- What sits on 953101 itself: dropped where the renamed page already sells
-- the furni, moved onto it otherwise.
DROP TABLE IF EXISTS `_bc_have`;
CREATE TABLE `_bc_have` (`type` VARCHAR(2) NOT NULL, `sprite_id` INT NOT NULL,
    PRIMARY KEY (`type`, `sprite_id`)) ENGINE=InnoDB;
INSERT IGNORE INTO `_bc_have` (`type`, `sprite_id`)
    SELECT f.`type`, f.`sprite_id`
      FROM `catalog_items` ci JOIN `furniture` f ON CAST(f.`id` AS CHAR) = ci.`item_id`
     WHERE ci.`page_id` = @blocks;

DELETE ci FROM `catalog_items` ci
  JOIN `furniture` f ON CAST(f.`id` AS CHAR) = ci.`item_id`
  JOIN `_bc_have` h ON h.`type` = f.`type` AND h.`sprite_id` = f.`sprite_id`
 WHERE ci.`page_id` = 953101 AND @blocks IS NOT NULL;

UPDATE `catalog_items` SET `page_id` = @blocks WHERE `page_id` = 953101 AND @blocks IS NOT NULL;

DROP TABLE `_bc_have`;

-- 953101 once it holds nothing, then its divider.
DELETE FROM `catalog_pages`
 WHERE `id` = 953101 AND @blocks IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM `catalog_items` WHERE `page_id` = 953101)
   AND NOT EXISTS (SELECT 1 FROM (SELECT `parent_id` FROM `catalog_pages`) c WHERE c.`parent_id` = 953101);
DELETE FROM `catalog_pages`
 WHERE `id` = 953100
   AND NOT EXISTS (SELECT 1 FROM (SELECT `id` FROM `catalog_pages`) c WHERE c.`id` = 953101);

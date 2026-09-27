-- pixelrp: Corporations goes to the bottom of PixelRP, and Builders Club to the
-- top of Construction.
--
-- 204 put Builders Club last under the PixelRP heading, below Turfs; it is
-- Habbo's own building set, so it moves down to lead the Construction group
-- instead, and Corporations takes the last place under PixelRP. The tab reads
--
--     PIXELRP
--     Information
--     Roomad
--     Infrastructure
--     Turfs
--     ...anything else under PixelRP, in the order it had
--     Corporations
--     CONSTRUCTION
--     Builders Club
--     Customs
--     Designer
--     ...
--     HABBO
--     Themes
--     Seasonal
--
-- Each page's group is where it stands now - before the Construction heading,
-- between it and the Habbo heading, or after that - except the two moved.
-- The named pages take their places in their group and everything else keeps
-- the order it has, so a page that exists in no migration stays in its group.
-- The top of the tab exists in no migration, so pages are found by caption
-- directly under Builders, as 204 found them; one that is not there is skipped.
--
-- Idempotent: renumbering an ordered tab changes nothing.

SET @builders := (SELECT `id` FROM `catalog_pages` WHERE `parent_id` = -1 AND `caption` = 'Builders' LIMIT 1);
SET @construction := COALESCE(
    (SELECT `id` FROM `catalog_pages` WHERE `id` = 943000 AND `parent_id` = @builders LIMIT 1),
    (SELECT `id` FROM `catalog_pages` WHERE `parent_id` = @builders AND `caption` = 'Construction' ORDER BY `id` LIMIT 1));
SET @habbo := (SELECT `id` FROM `catalog_pages` WHERE `id` = 953000 AND `parent_id` = @builders LIMIT 1);
SET @construction_order := COALESCE((SELECT `order_num` FROM `catalog_pages` WHERE `id` = @construction), 1000000);
SET @habbo_order := COALESCE((SELECT `order_num` FROM `catalog_pages` WHERE `id` = @habbo), 1000000);

-- group 0 PixelRP, 1 Construction, 2 Habbo; pos inside the group
DROP TABLE IF EXISTS `_pc_place`;
CREATE TABLE `_pc_place` (`id` INT NOT NULL PRIMARY KEY, `grp` INT NOT NULL, `pos` INT NOT NULL) ENGINE=InnoDB;
INSERT IGNORE INTO `_pc_place` (`id`, `grp`, `pos`)
    SELECT `id`, 0, 0 FROM `catalog_pages` WHERE `id` = 956000 AND `parent_id` = @builders;
INSERT IGNORE INTO `_pc_place` (`id`, `grp`, `pos`)
    SELECT `id`, 0, 1 FROM `catalog_pages` WHERE `parent_id` = @builders AND `caption` = 'Information' ORDER BY `id` LIMIT 1;
INSERT IGNORE INTO `_pc_place` (`id`, `grp`, `pos`)
    SELECT `id`, 0, 2 FROM `catalog_pages` WHERE `parent_id` = @builders AND `caption` = 'Roomad' ORDER BY `id` LIMIT 1;
INSERT IGNORE INTO `_pc_place` (`id`, `grp`, `pos`)
    SELECT `id`, 0, 3 FROM `catalog_pages` WHERE `parent_id` = @builders AND `caption` = 'Infrastructure' ORDER BY `id` LIMIT 1;
INSERT IGNORE INTO `_pc_place` (`id`, `grp`, `pos`)
    SELECT `id`, 0, 4 FROM `catalog_pages` WHERE `parent_id` = @builders AND `caption` = 'Turfs' ORDER BY `id` LIMIT 1;
INSERT IGNORE INTO `_pc_place` (`id`, `grp`, `pos`)
    SELECT `id`, 0, 6 FROM `catalog_pages` WHERE `parent_id` = @builders AND `caption` = 'Corporations' ORDER BY `id` LIMIT 1;
INSERT IGNORE INTO `_pc_place` (`id`, `grp`, `pos`)
    SELECT `id`, 1, 0 FROM `catalog_pages` WHERE `id` = @construction;
INSERT IGNORE INTO `_pc_place` (`id`, `grp`, `pos`)
    SELECT `id`, 1, 1 FROM `catalog_pages` WHERE `parent_id` = @builders AND `caption` = 'Builders Club' ORDER BY `id` LIMIT 1;
INSERT IGNORE INTO `_pc_place` (`id`, `grp`, `pos`)
    SELECT `id`, 1, 2 FROM `catalog_pages` WHERE `parent_id` = @builders AND `caption` = 'Customs' ORDER BY `id` LIMIT 1;
INSERT IGNORE INTO `_pc_place` (`id`, `grp`, `pos`)
    SELECT `id`, 1, 3 FROM `catalog_pages` WHERE `parent_id` = @builders AND `caption` IN ('Designer', 'Designer Furni') ORDER BY `id` LIMIT 1;
INSERT IGNORE INTO `_pc_place` (`id`, `grp`, `pos`)
    SELECT `id`, 2, 0 FROM `catalog_pages` WHERE `id` = @habbo;
INSERT IGNORE INTO `_pc_place` (`id`, `grp`, `pos`)
    SELECT `id`, 2, 1 FROM `catalog_pages` WHERE `parent_id` = @builders AND `caption` = 'Themes' ORDER BY `id` LIMIT 1;
INSERT IGNORE INTO `_pc_place` (`id`, `grp`, `pos`)
    SELECT `id`, 2, 2 FROM `catalog_pages` WHERE `parent_id` = @builders AND `caption` = 'Seasonal' ORDER BY `id` LIMIT 1;

-- Everything else stays in the group it stands in, after that group's named
-- pages - except in PixelRP, where Corporations (pos 6) stays last.
UPDATE `catalog_pages` p
  JOIN (
      SELECT c.`id`,
             ROW_NUMBER() OVER (ORDER BY
                 COALESCE(t.`grp`, CASE WHEN c.`order_num` < @construction_order THEN 0
                                        WHEN c.`order_num` < @habbo_order THEN 1
                                        ELSE 2 END),
                 COALESCE(t.`pos`, 5),
                 c.`order_num`, c.`id`) AS `rn`
        FROM `catalog_pages` c
        LEFT JOIN `_pc_place` t ON t.`id` = c.`id`
       WHERE c.`parent_id` = @builders
  ) o ON o.`id` = p.`id`
   SET p.`order_num` = o.`rn`;

DROP TABLE `_pc_place`;

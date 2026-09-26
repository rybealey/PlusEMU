-- pixelrp: Misc's leftovers go to the furni line they belong to.
--
-- After 182, what is still on Furni > Misc has no furniline in FurnitureData,
-- which is exactly why nothing could place it. Most of it plainly belongs to
-- a line that has a page, so each is placed by hand here, by sprite:
--
--   Lines > Classics        pizza box, empty cans, floor tile, Norja stool,
--                           clothes rack, notice board, trading table, door
--                           teleports, stickies, the Habbo rollers
--   Themes > Household      the TVs and computers, the mood light and its
--                           switches
--   Staff > Games           the ticket bundle and vote machines (More Games);
--                           the score boards (Score Boards)
--   Staff > Sound / Wired   the sound machine and sound blocks; the wired
--                           test conditions
--   Staff > Rares           the Parasol
--   Seasonal > Valentine's  the 2011 Roller Rink pieces
--   Themes > ...            Guilds, Background, Art, Pixelrp, Pirates,
--                           School, Cyberpunk, Festival, Ice Cream Parlor,
--                           Circus - one piece or a few each
--
-- 77 furni, 20 paths. A path is resolved by caption on open pages from its
-- tab, falling back as listed; one that does not resolve leaves the furni on
-- Misc. What has no line at all - the teleport and state-storage tests, the
-- legacy wall games with no bundle - stays on Misc. A furni the target page
-- already sells is deleted instead of moved in twice.
--
-- EACH IS RENAMED as it moves, to the name its line sells it under: the
-- furniture's own name where that is fine, a tidied one where it is not
-- ('Norja-pehmojakkara' is Norja Stool), and for a test copy of a real furni
-- the real one's name (pirate_barrel2_test is the Orange Barrel). Both the
-- catalog row and the furniture row's public_name take it.
--
-- A TEST COPY WHOSE REAL FURNI IS ALREADY ON THE TARGET PAGE GOES instead of
-- moving in beside it - the same furni twice, under one name. Where the page
-- does not have it, the copy moves in and stands in for it.
--
-- Idempotent: only rows still on Misc move.

SET @furni := (SELECT `id` FROM `catalog_pages` WHERE `parent_id` = -1 AND `page_link` = 'furni' LIMIT 1);
SET @builders := (SELECT `id` FROM `catalog_pages` WHERE `parent_id` = -1 AND `caption` = 'Builders' LIMIT 1);
SET @staff := (SELECT `id` FROM `catalog_pages` WHERE `parent_id` = -1 AND `page_link` = 'staff' LIMIT 1);
SET @petshop := (SELECT `id` FROM `catalog_pages` WHERE `parent_id` = -1 AND `page_link` = 'pets_shop' LIMIT 1);
SET @misc := (SELECT `id` FROM `catalog_pages` WHERE `parent_id` = @furni AND `caption` = 'Misc' LIMIT 1);

DROP TABLE IF EXISTS `_ml_path`;
CREATE TABLE `_ml_path` (
    `path_id` INT NOT NULL PRIMARY KEY, `tab` CHAR(1) NOT NULL,
    `p1` VARCHAR(35) NOT NULL, `p2` VARCHAR(35) NULL, `p3` VARCHAR(35) NULL,
    `page_id` INT NULL) ENGINE=InnoDB DEFAULT CHARSET=latin1;
INSERT INTO `_ml_path` (`path_id`,`tab`,`p1`,`p2`,`p3`) VALUES
(1,'F','Lines','Classics',NULL),
(2,'B','Themes','Household',NULL),
(3,'S','Games','More Games',NULL),
(4,'S','Sound',NULL,NULL),
(5,'S','Rares','Rares',NULL),
(6,'S','Rares',NULL,NULL),
(7,'B','Seasonal','Valentine\'s','Valentine\'s 2011'),
(8,'B','Seasonal','Valentine\'s',NULL),
(9,'B','Themes','Guilds',NULL),
(10,'B','Themes','Background',NULL),
(11,'S','Wired',NULL,NULL),
(12,'B','Themes','Ice Cream Parlor',NULL),
(13,'B','Themes','Circus',NULL),
(14,'S','Games','Score Boards',NULL),
(15,'B','Themes','Festival',NULL),
(16,'B','Themes','Pirates',NULL),
(17,'B','Themes','School',NULL),
(18,'B','Themes','Pixelrp',NULL),
(19,'B','Themes','Cyberpunk',NULL),
(20,'B','Themes','Art',NULL);

DROP TABLE IF EXISTS `_ml_map`;
CREATE TABLE `_ml_map` (
    `type` VARCHAR(2) NOT NULL, `sprite_id` INT NOT NULL, `priority` INT NOT NULL, `path_id` INT NOT NULL,
    PRIMARY KEY (`type`, `sprite_id`, `priority`)) ENGINE=InnoDB;
INSERT INTO `_ml_map` (`type`,`sprite_id`,`priority`,`path_id`) VALUES
('s',122,0,1),
('s',123,0,1),
('s',132,0,1),
('s',144,0,2),
('s',145,0,2),
('s',173,0,2),
('s',420,0,1),
('s',1649,0,1),
('s',1650,0,1),
('s',1651,0,1),
('s',1652,0,1),
('s',1653,0,1),
('s',1654,0,1),
('s',1655,0,1),
('s',1656,0,1),
('s',1657,0,1),
('s',1658,0,1),
('s',1659,0,3),
('s',2105,0,4),
('s',2122,0,5),
('s',2122,1,6),
('s',2693,0,1),
('s',3184,0,1),
('s',3186,0,1),
('s',3804,0,7),
('s',3804,1,8),
('s',3806,0,7),
('s',3806,1,8),
('s',3812,0,7),
('s',3812,1,8),
('s',3886,0,2),
('s',3888,0,2),
('s',3893,0,2),
('s',3894,0,2),
('s',4249,0,9),
('s',5337,0,10),
('s',5368,0,11),
('s',5369,0,11),
('s',5376,0,11),
('s',5378,0,3),
('s',5379,0,11),
('s',5381,0,11),
('s',5385,0,11),
('s',5386,0,12),
('s',5387,0,13),
('s',5388,0,11),
('s',5390,0,11),
('s',5392,0,11),
('s',5394,0,14),
('s',5395,0,14),
('s',5396,0,14),
('s',5397,0,14),
('s',5401,0,14),
('s',5402,0,14),
('s',5403,0,14),
('s',5404,0,14),
('s',5416,0,3),
('s',5430,0,11),
('s',5431,0,4),
('s',5433,0,15),
('s',5435,0,4),
('s',11954,0,1),
('s',18092,0,16),
('s',18093,0,16),
('s',18094,0,17),
('s',18095,0,17),
('s',18096,0,17),
('s',100001,0,18),
('s',100002,0,18),
('s',100003,0,18),
('s',100004,0,18),
('s',100005,0,18),
('i',1,0,1),
('i',2,0,1),
('i',4027,0,2),
('i',4237,0,2),
('i',4238,0,2),
('i',4239,0,2),
('i',4240,0,2),
('i',4601,0,19),
('i',4710,0,20);

UPDATE `_ml_path` x
  JOIN `catalog_pages` a ON a.`caption` = x.`p1` AND a.`visible` = b'1' AND a.`enabled` = b'1'
   AND a.`parent_id` = CASE x.`tab` WHEN 'F' THEN @furni WHEN 'B' THEN @builders WHEN 'S' THEN @staff ELSE @petshop END
  LEFT JOIN `catalog_pages` b ON x.`p2` IS NOT NULL AND b.`parent_id` = a.`id` AND b.`caption` = x.`p2`
   AND b.`visible` = b'1' AND b.`enabled` = b'1'
  LEFT JOIN `catalog_pages` c ON x.`p3` IS NOT NULL AND c.`parent_id` = b.`id` AND c.`caption` = x.`p3`
   AND c.`visible` = b'1' AND c.`enabled` = b'1'
   SET x.`page_id` = CASE
        WHEN x.`p3` IS NOT NULL THEN c.`id`
        WHEN x.`p2` IS NOT NULL THEN b.`id`
        ELSE a.`id` END;

DROP TABLE IF EXISTS `_ml_name`;
CREATE TABLE `_ml_name` (`type` VARCHAR(2) NOT NULL, `sprite_id` INT NOT NULL, `name` VARCHAR(100) NOT NULL,
    PRIMARY KEY (`type`, `sprite_id`)) ENGINE=InnoDB DEFAULT CHARSET=latin1;
INSERT INTO `_ml_name` (`type`,`sprite_id`,`name`) VALUES
('s',122,'Pizza Box'),
('s',123,'Empty Cans'),
('s',132,'Floor Tile'),
('s',144,'Portable TV'),
('s',145,'Large TV'),
('s',173,'Digital TV'),
('s',420,'Norja Stool'),
('s',1649,'Aqua Habbo Roller'),
('s',1650,'Blue Habbo Roller'),
('s',1651,'Gold Habbo Roller'),
('s',1652,'Green Habbo Roller'),
('s',1653,'Teal Habbo Roller'),
('s',1654,'Black Habbo Roller'),
('s',1655,'Purple Habbo Roller'),
('s',1656,'Red Habbo Roller'),
('s',1657,'Pink Habbo Roller'),
('s',1658,'Silver Habbo Roller'),
('s',1659,'Big Ticket Bundle'),
('s',2105,'Sound Machine'),
('s',2122,'Parasol'),
('s',2693,'Door Teleport'),
('s',3184,'Clothes Rack'),
('s',3186,'Notice Board'),
('s',3804,'Boom Box'),
('s',3806,'Roller Rink Chair'),
('s',3812,'Roller Rink Railing'),
('s',3886,'Flatscreen TV'),
('s',3888,'Desktop Computer'),
('s',3893,'Laptop'),
('s',3894,'Nostalgic Computer'),
('s',4249,'Group Banner'),
('s',5337,'Background Colour'),
('s',5368,'WIRED Condition: Furni DOESN\'T Match'),
('s',5369,'WIRED Negative Condition: Has NO Furni On'),
('s',5376,'WIRED Negative Condition: Furnis have NO avatars'),
('s',5378,'Majority Vote Machine'),
('s',5379,'WIRED Negative Condition: NOT Group Member'),
('s',5381,'WIRED Negative Condition: Furni states DOESN\'T match'),
('s',5385,'WIRED Negative Condition: NOT Team Member'),
('s',5386,'Ice Cream Stand'),
('s',5387,'Cotton Candy Stand'),
('s',5388,'WIRED Negative Condition: NOT Wearing Badge'),
('s',5390,'WIRED Negative Condition: NOT Wearing Effect'),
('s',5392,'WIRED Negative Condition: Triggerer is NOT on furni'),
('s',5394,'Highscore - Alltime'),
('s',5395,'Highscore - Daily'),
('s',5396,'Highscore - Weekly'),
('s',5397,'Highscore - Monthly'),
('s',5401,'Highscore Wins - Alltime'),
('s',5402,'Highscore Wins - Daily'),
('s',5403,'Highscore Wins - Weekly'),
('s',5404,'Highscore Wins - Monthly'),
('s',5416,'Vote Machine'),
('s',5430,'WIRED Condition: User DOESN\'T count in Room'),
('s',5431,'Sound Block'),
('s',5433,'Hot Dog Vendor'),
('s',5435,'Breakbeat Sound Block'),
('s',11954,'Trading Table'),
('s',18092,'Fish Barrel'),
('s',18093,'Orange Barrel'),
('s',18094,'Cafeteria Burger'),
('s',18095,'Cafeteria Nuggets'),
('s',18096,'Cafeteria Meatballs'),
('s',100001,'Action Point'),
('s',100002,'Turf Arrow'),
('s',100003,'Teleport Arrow (White)'),
('s',100004,'Teleport Arrow'),
('s',100005,'Taxi Sign'),
('i',1,'Sticky Pad'),
('i',2,'Heart Sticky Pad'),
('i',4027,'Mood Light'),
('i',4237,'Large Mood Switch'),
('i',4238,'Small Mood Switch'),
('i',4239,'Small Mood Controller'),
('i',4240,'Large Mood Controller'),
('i',4601,'Neo-Habbo Cityscape'),
('i',4710,'Duck Poster');

DROP TABLE IF EXISTS `_ml_real`;
CREATE TABLE `_ml_real` (`type` VARCHAR(2) NOT NULL, `sprite_id` INT NOT NULL, `real_sprite` INT NOT NULL,
    PRIMARY KEY (`type`, `sprite_id`)) ENGINE=InnoDB;
INSERT INTO `_ml_real` (`type`,`sprite_id`,`real_sprite`) VALUES
('s',5368,5449),
('s',5369,5440),
('s',5376,5441),
('s',5379,5448),
('s',5381,5452),
('s',5385,5439),
('s',5387,5153),
('s',5388,5446),
('s',5390,5444),
('s',5392,5438),
('s',5394,5044),
('s',5395,5045),
('s',5396,5046),
('s',5397,5047),
('s',5430,5443),
('s',5433,5156),
('s',18092,5238),
('s',18093,5233),
('s',18094,4161),
('s',18095,4156),
('s',18096,4160);

DROP TABLE IF EXISTS `_ml_move`;
CREATE TABLE `_ml_move` (`id` INT NOT NULL PRIMARY KEY, `sprite_type` VARCHAR(2) NOT NULL,
    `sprite_id` INT NOT NULL, `target` INT NOT NULL) ENGINE=InnoDB;
INSERT INTO `_ml_move` (`id`, `sprite_type`, `sprite_id`, `target`)
SELECT ci.`id`, f.`type`, f.`sprite_id`, t.`page_id`
  FROM `catalog_items` ci
  JOIN `furniture` f ON CAST(f.`id` AS CHAR) = ci.`item_id`
  JOIN (SELECT m.`type`, m.`sprite_id`,
               SUBSTRING_INDEX(GROUP_CONCAT(x.`page_id` ORDER BY m.`priority`), ',', 1) AS `page_id`
          FROM `_ml_map` m JOIN `_ml_path` x ON x.`path_id` = m.`path_id`
         WHERE x.`page_id` IS NOT NULL
         GROUP BY m.`type`, m.`sprite_id`) t ON t.`type` = f.`type` AND t.`sprite_id` = f.`sprite_id`
 WHERE ci.`page_id` = @misc;

DROP TABLE IF EXISTS `_ml_have`;
CREATE TABLE `_ml_have` (`page_id` INT NOT NULL, `type` VARCHAR(2) NOT NULL, `sprite_id` INT NOT NULL,
    PRIMARY KEY (`page_id`, `type`, `sprite_id`)) ENGINE=InnoDB;
INSERT IGNORE INTO `_ml_have` (`page_id`, `type`, `sprite_id`)
    SELECT ci.`page_id`, f.`type`, f.`sprite_id`
      FROM `catalog_items` ci JOIN `furniture` f ON CAST(f.`id` AS CHAR) = ci.`item_id`
     WHERE ci.`page_id` IN (SELECT DISTINCT `target` FROM `_ml_move`);
DELETE ci FROM `catalog_items` ci
  JOIN `_ml_move` mv ON mv.`id` = ci.`id`
  JOIN `_ml_have` h ON h.`page_id` = mv.`target` AND h.`type` = mv.`sprite_type` AND h.`sprite_id` = mv.`sprite_id`;
-- a test copy whose real furni the target already sells
DELETE ci FROM `catalog_items` ci
  JOIN `_ml_move` mv ON mv.`id` = ci.`id`
  JOIN `_ml_real` r ON r.`type` = mv.`sprite_type` AND r.`sprite_id` = mv.`sprite_id`
  JOIN `_ml_have` h ON h.`page_id` = mv.`target` AND h.`type` = mv.`sprite_type` AND h.`sprite_id` = r.`real_sprite`;
DELETE ci FROM `catalog_items` ci
  JOIN `_ml_move` mv ON mv.`id` = ci.`id`
  JOIN `_ml_move` older ON older.`target` = mv.`target` AND older.`sprite_type` = mv.`sprite_type`
   AND older.`sprite_id` = mv.`sprite_id` AND older.`id` < mv.`id`;

UPDATE `catalog_items` ci
  JOIN `_ml_move` mv ON mv.`id` = ci.`id`
  JOIN `_ml_name` n ON n.`type` = mv.`sprite_type` AND n.`sprite_id` = mv.`sprite_id`
   SET ci.`page_id` = mv.`target`, ci.`catalog_name` = n.`name`;
UPDATE `furniture` f
  JOIN `catalog_items` ci ON CAST(f.`id` AS CHAR) = ci.`item_id`
  JOIN `_ml_move` mv ON mv.`id` = ci.`id`
  JOIN `_ml_name` n ON n.`type` = mv.`sprite_type` AND n.`sprite_id` = mv.`sprite_id`
   SET f.`public_name` = LEFT(n.`name`, 56);

DROP TABLE `_ml_have`;
DROP TABLE `_ml_real`;
DROP TABLE `_ml_name`;
DROP TABLE `_ml_move`;
DROP TABLE `_ml_map`;
DROP TABLE `_ml_path`;

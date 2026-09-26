-- pixelrp: every page under Builders > Customs wears an icon of its own furni,
-- and Customs itself a hammer and wrench.
--
-- Customs' 114 sets and sub-sets all showed 193, the hard hat. Each now shows
-- one of its own furni, chosen by eye for the piece that says what the set is
-- (the owl for Animals HP, a poker chip for Poker, the fuel pump for Fuel
-- Station), fitted to 18x18 and named after its sprite as 197's are:
-- icon_<7000000 + sprite>.png under nitro/overrides/c_images/catalogue/.
-- A page with sub-pages wears a piece none of them wears - the snitch over
-- Harry Potter, a mushroom over Mario, Hello Kitty over Sanrio - and Customs
-- wears stock icon 79, which no set uses, so no category shares an icon with
-- the one above or below it.
--
-- Pages by fixed id (94, 122, 139, 165, 166, 59's Cartier, 93's Tiles), and only
-- while they sit under Customs. Idempotent.

SET @builders := (SELECT `id` FROM `catalog_pages` WHERE `parent_id` = -1 AND `caption` = 'Builders' LIMIT 1);
SET @customs := COALESCE(
    (SELECT `id` FROM `catalog_pages` WHERE `id` = 944000 AND `parent_id` = @builders LIMIT 1),
    (SELECT `id` FROM `catalog_pages` WHERE `parent_id` = @builders AND `caption` = 'Customs' ORDER BY `id` LIMIT 1));

DROP TABLE IF EXISTS `_ci_pages`;
CREATE TABLE `_ci_pages` (`id` INT NOT NULL PRIMARY KEY) ENGINE=InnoDB;
INSERT IGNORE INTO `_ci_pages` (`id`) SELECT `id` FROM `catalog_pages` WHERE `parent_id` = @customs;
INSERT IGNORE INTO `_ci_pages` (`id`)
    SELECT c.`id` FROM `catalog_pages` c JOIN `_ci_pages` p ON c.`parent_id` = p.`id`;
INSERT IGNORE INTO `_ci_pages` (`id`)
    SELECT c.`id` FROM `catalog_pages` c JOIN `_ci_pages` p ON c.`parent_id` = p.`id`;

DROP TABLE IF EXISTS `_ci_icon`;
CREATE TABLE `_ci_icon` (`page_id` INT NOT NULL PRIMARY KEY, `icon` INT NOT NULL) ENGINE=InnoDB;
INSERT INTO `_ci_icon` (`page_id`, `icon`) VALUES
    (944001, 7104553),
    (944002, 7104578),
    (944003, 7104586),
    (951000, 7110617),
    (944004, 7104600),
    (944005, 7104605),
    (944006, 7104630),
    (944007, 7104642),
    (945000, 7108972),
    (945001, 7108179),
    (945002, 7108987),
    (945003, 7109058),
    (945004, 7109097),
    (945005, 7109178),
    (945006, 7109325),
    (945007, 7109472),
    (945008, 7109588),
    (945009, 7109659),
    (945010, 7109787),
    (945011, 7109880),
    (944008, 7104665),
    (912375, 7100014),
    (944009, 7104720),
    (944010, 7104730),
    (944011, 7104747),
    (944012, 7104763),
    (944013, 7104925),
    (944014, 7104766),
    (944015, 7104770),
    (944016, 7104847),
    (944017, 7104896),
    (944018, 7104911),
    (944019, 7104934),
    (944020, 7104935),
    (947000, 7109916),
    (944021, 7104942),
    (944022, 7104947),
    (944023, 7105011),
    (944024, 7105028),
    (944025, 7105031),
    (944026, 7105047),
    (944027, 7105531),
    (944028, 7105060),
    (944029, 7105076),
    (944030, 7105096),
    (944031, 7105138),
    (944032, 7105158),
    (944033, 7105166),
    (944034, 7105231),
    (944035, 7105275),
    (944036, 7105278),
    (944037, 7105293),
    (944038, 7105311),
    (944039, 7105332),
    (944040, 7105335),
    (944041, 7105345),
    (944042, 7105384),
    (944043, 7105409),
    (944044, 7105412),
    (944045, 7105446),
    (944046, 7105467),
    (944047, 7105516),
    (944048, 7105525),
    (944049, 7105561),
    (944050, 7105605),
    (944051, 7105618),
    (944052, 7105640),
    (944053, 7105786),
    (944054, 7105855),
    (944055, 7105891),
    (944056, 7105905),
    (944057, 7105958),
    (944058, 7105978),
    (944059, 7106005),
    (944060, 7106024),
    (944061, 7106058),
    (944062, 7106027),
    (944063, 7106061),
    (944064, 7106094),
    (944065, 7106106),
    (944066, 7106118),
    (944067, 7106124),
    (944068, 7106190),
    (944069, 7106194),
    (944070, 7106222),
    (944071, 7106258),
    (944072, 7106279),
    (944073, 7106291),
    (944074, 7106320),
    (944075, 7106393),
    (944076, 7106407),
    (944077, 7106416),
    (944078, 7106415),
    (944079, 7106436),
    (944080, 7106453),
    (944081, 7106466),
    (944082, 7106460),
    (944083, 7106469),
    (944084, 7106486),
    (944085, 7106499),
    (944086, 7106515),
    (944087, 7106526),
    (943070, 7104527),
    (944088, 7106554),
    (944089, 7106562),
    (944090, 7106650),
    (944091, 7106586),
    (944092, 7106648),
    (944093, 7106710),
    (944094, 7106721),
    (944095, 7106720),
    (944096, 7106723),
    (944097, 7106753),
    (952000, 7110700);

UPDATE `catalog_pages` p
  JOIN `_ci_pages` s ON s.`id` = p.`id`
  JOIN `_ci_icon` i ON i.`page_id` = p.`id`
   SET p.`icon_image` = i.`icon`;

UPDATE `catalog_pages` SET `icon_image` = 79 WHERE `id` = @customs;

DROP TABLE `_ci_icon`;
DROP TABLE `_ci_pages`;

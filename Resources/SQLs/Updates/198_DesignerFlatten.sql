-- pixelrp: Builders > Designer loses the sub-categories that only cost a click.
--
-- 92 laid Designer out as the download folder was: a page per designer, a
-- page per pack under it. Three shapes of that were an extra click for
-- nothing, and each is folded into the page above it - its furni move up and
-- the emptied page goes:
--
--   1. twenty designers whose page held nothing but ONE pack;
--   2. three whose page held loose furni beside one pack (Alaska, Breana,
--      Fbnncls) - you landed on the loose ones and had to spot the pack;
--   3. Mathias's two three-level branches: Decoration I and II fold into
--      Decorations, Plants I and II into Plants.
--
-- The 27 folds, page -> the page it folds into:
--   Beach -> Styx
--   Bunny Cafe -> Shtrudel
--   Cheapyxo House -> Haaziq
--   Cove -> Duda
--   Cozy Winter -> Bebisitas
--   Deluxe -> Huan
--   Flower -> Arcanyan
--   Flower Power -> Lea Beauty
--   Fortress -> Mario
--   Household -> XSongzi
--   Karma -> Soleau
--   Modern Style -> Isa
--   Oriental Garden -> Fufu
--   Pastel -> Vaulted
--   Pink Cottage -> Melek
--   Polars -> Sparwari
--   Sporting Goods -> Hellsinore
--   Street -> Pablo Vittar
--   Studio -> Alpisco
--   Witchcraft -> Malin
--   Gum -> Breana
--   Kattegat -> Alaska
--   Modern Kitchen -> Fbnncls
--   Decoration I -> Mathias > Decorations
--   Decoration II -> Mathias > Decorations
--   Plants I -> Mathias > Plants
--   Plants II -> Mathias > Plants
--
-- By 92's fixed ids, and each only while the page still sits under the one
-- it folds into, so nothing rearranged on the server since can be caught.
-- The furni keep their rows; a designer's page lists them in the order 92
-- inserted them. Idempotent: a second run finds the pages gone.

DROP TABLE IF EXISTS `_df_fold`;
CREATE TABLE `_df_fold` (`from_page` INT NOT NULL PRIMARY KEY, `to_page` INT NOT NULL) ENGINE=InnoDB;
INSERT INTO `_df_fold` (`from_page`, `to_page`) VALUES
    (942148, 942147),
    (942123, 942122),
    (942037, 942036),
    (942022, 942021),
    (942013, 942012),
    (942044, 942043),
    (942005, 942004),
    (942061, 942060),
    (942075, 942074),
    (942161, 942160),
    (942139, 942138),
    (942046, 942045),
    (942034, 942033),
    (942151, 942150),
    (942091, 942090),
    (942141, 942140),
    (942042, 942041),
    (942117, 942116),
    (942003, 942002),
    (942073, 942072),
    (942015, 942014),
    (942001, 942000),
    (942027, 942026),
    (942082, 942081),
    (942083, 942081),
    (942086, 942085),
    (942087, 942085);

-- only folds whose page still sits where 92 put it
DELETE f FROM `_df_fold` f
  LEFT JOIN `catalog_pages` p ON p.`id` = f.`from_page`
 WHERE p.`id` IS NULL OR p.`parent_id` <> f.`to_page`;

UPDATE `catalog_items` ci JOIN `_df_fold` f ON f.`from_page` = ci.`page_id`
   SET ci.`page_id` = f.`to_page`;

DELETE p FROM `catalog_pages` p JOIN `_df_fold` f ON f.`from_page` = p.`id`
 WHERE NOT EXISTS (SELECT 1 FROM `catalog_items` ci WHERE ci.`page_id` = p.`id`)
   AND NOT EXISTS (SELECT 1 FROM (SELECT `parent_id` FROM `catalog_pages`) c WHERE c.`parent_id` = p.`id`);

DROP TABLE `_df_fold`;

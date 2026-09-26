-- pixelrp: Construction's alphabets go to Builders > Builders Club.
--
-- 1. CONSTRUCTION > ALPHABET WAS A DUPLICATE. Its letter blocks
--    (bc_alpha1_a-z and bc_alpha1_num, furniture 104297-104323) draw from the
--    same bundles as Builders Club's own letters (bc_alpha1_a*1-*14 and so
--    on) - nitro/overrides/bundled/furniture/bc_alpha1_a.nitro serves both -
--    and Builders Club's change colour in the room, so they already are every
--    colour Construction's plain white ones come in. Those 27 rows come off
--    the shop. The furniture rows, and every one anybody owns, are untouched.
--
--    Its two pieces with no counterpart - Numbers 1-99 and Numbers, Letters &
--    Symbols (104324, 104325) - move to Builders Club > Alphabet: the
--    server's own page of that name if it has one (that tab's pages exist in
--    no migration), else 182's Letters & Numbers (953104), renamed Alphabet.
--    Construction > Alphabet (943001), then empty, goes.
--
-- 2. CONSTRUCTION > CUSTOM ALPHABETS (943047) moves under Builders Club
--    whole, its 22 pieces with it, after the pages already there.
--
-- Nothing moves, and nothing is deleted, if Builders Club is not there.
-- Idempotent: a second run finds the letters gone, 943001 gone and 943047
-- already under Builders Club.

SET @builders := (SELECT `id` FROM `catalog_pages` WHERE `parent_id` = -1 AND `caption` = 'Builders' LIMIT 1);
SET @bc := (SELECT `id` FROM `catalog_pages` WHERE `parent_id` = @builders AND `caption` = 'Builders Club' ORDER BY `id` LIMIT 1);
SET @live_alpha := (SELECT `id` FROM `catalog_pages` WHERE `parent_id` = @bc AND `caption` = 'Alphabet' AND `id` <> 943001 ORDER BY `id` LIMIT 1);
SET @letters := (SELECT `id` FROM `catalog_pages` WHERE `id` = 953104 AND `parent_id` = @bc LIMIT 1);
SET @target := COALESCE(@live_alpha, @letters);
SET @bc_last := (SELECT COALESCE(MAX(`order_num`), 0) FROM `catalog_pages` WHERE `parent_id` = @bc);

-- 1. The duplicate letter blocks come off the shop.
DELETE FROM `catalog_items`
 WHERE `item_id` IN ('104297','104298','104299','104300','104301','104302','104303','104304','104305',
                     '104306','104307','104308','104309','104310','104311','104312','104313','104314',
                     '104315','104316','104317','104318','104319','104320','104321','104322','104323')
   AND @bc IS NOT NULL;

-- No Alphabet under Builders Club: Letters & Numbers becomes it.
UPDATE `catalog_pages` SET `caption` = 'Alphabet' WHERE `id` = @letters AND @live_alpha IS NULL;

-- The two number sets move over, unless the page already sells them.
DELETE ci FROM `catalog_items` ci
  JOIN `furniture` f ON CAST(f.`id` AS CHAR) = ci.`item_id`
 WHERE ci.`page_id` = 943001 AND @target IS NOT NULL
   AND EXISTS (SELECT 1 FROM (SELECT c2.`page_id`, f2.`type`, f2.`sprite_id`
                                FROM `catalog_items` c2 JOIN `furniture` f2 ON CAST(f2.`id` AS CHAR) = c2.`item_id`) x
                WHERE x.`page_id` = @target AND x.`type` = f.`type` AND x.`sprite_id` = f.`sprite_id`);
UPDATE `catalog_items` SET `page_id` = @target WHERE `page_id` = 943001 AND @target IS NOT NULL;

DELETE p FROM `catalog_pages` p
 WHERE p.`id` = 943001 AND @target IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM `catalog_items` ci WHERE ci.`page_id` = p.`id`)
   AND NOT EXISTS (SELECT 1 FROM (SELECT `parent_id` FROM `catalog_pages`) c WHERE c.`parent_id` = p.`id`);

-- 2. Custom Alphabets, whole, to the end of Builders Club.
UPDATE `catalog_pages` SET `parent_id` = @bc, `order_num` = @bc_last + 1
 WHERE `id` = 943047 AND @bc IS NOT NULL AND `parent_id` <> @bc;

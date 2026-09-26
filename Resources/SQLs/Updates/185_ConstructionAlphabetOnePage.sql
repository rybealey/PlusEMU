-- pixelrp: Builders > Construction > Alphabet is one page, not 27.
--
-- 93 laid the alphabet out as its download folder was: a page per letter,
-- Letter A to Letter Z (943002-943027), and Numbers (943028), each holding a
-- single piece (Numbers three). Every piece moves onto Alphabet itself
-- (943001) - they keep their catalog rows, so they read A to Z and then the
-- numbers, in the order 93 inserted them - and the 27 emptied pages go.
--
-- Idempotent: a second run finds nothing on those pages and no pages to
-- delete. Nothing happens if Alphabet is not there.

SET @alphabet := (SELECT `id` FROM `catalog_pages` WHERE `id` = 943001 AND `caption` = 'Alphabet' LIMIT 1);

UPDATE `catalog_items` SET `page_id` = @alphabet
 WHERE `page_id` BETWEEN 943002 AND 943028 AND @alphabet IS NOT NULL;

DELETE p FROM `catalog_pages` p
 WHERE p.`id` BETWEEN 943002 AND 943028 AND p.`parent_id` = 943001 AND @alphabet IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM `catalog_items` ci WHERE ci.`page_id` = p.`id`)
   AND NOT EXISTS (SELECT 1 FROM (SELECT `parent_id` FROM `catalog_pages`) c WHERE c.`parent_id` = p.`id`);

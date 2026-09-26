-- pixelrp: Builders Club > Custom Alphabets sits right under Alphabet.
--
-- 188 moved Custom Alphabets (943047) under Builders Club at the end. It now
-- takes the place straight after Alphabet (the server's own, or the Letters &
-- Numbers page 188 renamed): every page after Alphabet moves down one, and
-- Custom Alphabets takes Alphabet's order + 1.
--
-- Idempotent: re-running gives the same order. Nothing changes if either page
-- is not under Builders Club.

SET @builders := (SELECT `id` FROM `catalog_pages` WHERE `parent_id` = -1 AND `caption` = 'Builders' LIMIT 1);
SET @bc := (SELECT `id` FROM `catalog_pages` WHERE `parent_id` = @builders AND `caption` = 'Builders Club' ORDER BY `id` LIMIT 1);
SET @alphabet := (SELECT `id` FROM `catalog_pages` WHERE `parent_id` = @bc AND `caption` = 'Alphabet' ORDER BY `id` LIMIT 1);
SET @custom := (SELECT `id` FROM `catalog_pages` WHERE `id` = 943047 AND `parent_id` = @bc LIMIT 1);
SET @alpha_order := (SELECT `order_num` FROM `catalog_pages` WHERE `id` = @alphabet);

UPDATE `catalog_pages` SET `order_num` = `order_num` + 1
 WHERE `parent_id` = @bc AND `order_num` > @alpha_order AND `id` <> @custom
   AND @custom IS NOT NULL AND @alpha_order IS NOT NULL;

UPDATE `catalog_pages` SET `order_num` = @alpha_order + 1
 WHERE `id` = @custom AND @alpha_order IS NOT NULL;

-- pixelrp: Builders > Images is renamed Roomad, with an icon of its own.
--
-- It is 130's page (946000) for the four room-ad furni - the room background
-- and the three billboard sizes, the pieces that load an image into the room.
-- It wore 193, the generic Builders icon; it now wears 254, a framed
-- billboard showing a picture, which is what those furni are.
--
-- Found by id, or by its old caption under Builders if the id ever differs.
-- Idempotent.

SET @builders := (SELECT `id` FROM `catalog_pages` WHERE `parent_id` = -1 AND `caption` = 'Builders' LIMIT 1);
SET @images := COALESCE(
    (SELECT `id` FROM `catalog_pages` WHERE `id` = 946000 LIMIT 1),
    (SELECT `id` FROM `catalog_pages` WHERE `parent_id` = @builders AND `caption` = 'Images' ORDER BY `id` LIMIT 1));

UPDATE `catalog_pages` SET `caption` = 'Roomad', `icon_image` = 254 WHERE `id` = @images;

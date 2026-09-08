-- PixelRP: the Chill Modern (darkmodern_c20) line becomes buyable.
--
-- All 28 pieces have existed as furniture since 31_FullFurniLibrary, but no
-- catalog row has ever pointed at them, so the only way to get one was for
-- staff to hand it over. This gives the line a page of its own under
-- Furni > Furni By Line, alongside the other ~60 lines.
--
-- Everything is 1 credit, as asked. Nothing charges duckets, per the
-- hotel-wide policy 83_CatalogRestoreDefault set.
--
-- Resolved by caption rather than by id: the restore renumbered every page it
-- rebuilt, so 'Furni By Line' is the only stable handle. Idempotent - the page
-- and its rows are rebuilt from scratch on re-apply.

SET @furni := (SELECT `id` FROM `catalog_pages` WHERE `parent_id` = -1 AND `page_link` = 'furni' LIMIT 1);
SET @line  := (SELECT `id` FROM `catalog_pages` WHERE `parent_id` = @furni AND `caption` = 'Furni By Line' LIMIT 1);

-- Nothing to hang the page off - a hotel whose catalog was never restored.
-- Every statement below is a no-op in that case rather than an error.
SET @page := (SELECT `id` FROM `catalog_pages` WHERE `parent_id` = @line AND `caption` = 'Chill Modern' LIMIT 1);
SET @page := COALESCE(@page, 960000);

DELETE FROM `catalog_items` WHERE `page_id` = @page;
DELETE FROM `catalog_pages` WHERE `id` = @page;

-- Appended to the end of the line list; the existing order is the default
-- dump's, which is not alphabetical, so there is no slot to insert into.
SET @order := (SELECT COALESCE(MAX(`order_num`), 0) + 1 FROM `catalog_pages` WHERE `parent_id` = @line);

INSERT INTO `catalog_pages`
    (`id`,`parent_id`,`caption`,`icon_image`,`min_rank`,`min_vip`,`order_num`,`page_link`,
     `page_layout`,`page_strings_1`,`page_strings_2`,`visible`,`enabled`)
-- No blurb: default_3x3 index-matches page_strings_1 (localisation keys)
-- against page_strings_2 (the text), and there are no keys for a page the
-- client's texts have never heard of. The Kasja pages are set up the same way.
SELECT @page, @line, 'Chill Modern', 39, 1, 0, @order, '', 'default_3x3', '', '', b'1', b'1'
FROM DUAL WHERE @line IS NOT NULL;

-- Selected rather than listed: the line is defined by its classname prefix, so
-- a piece added to `furniture` later is picked up by a re-apply.
INSERT INTO `catalog_items`
    (`page_id`,`item_id`,`catalog_name`,`cost_credits`,`cost_pixels`,`cost_diamonds`,
     `amount`,`limited_sells`,`limited_stack`,`offer_active`,`extradata`,`badge`,`offer_id`)
SELECT @page, CAST(f.`id` AS CHAR), f.`public_name`, 1, 0, 0, 1, 0, 0, '1', '', '', -1
FROM `furniture` f
WHERE @line IS NOT NULL
  AND f.`item_name` LIKE 'darkmodern\_c20\_%'
ORDER BY f.`public_name`;

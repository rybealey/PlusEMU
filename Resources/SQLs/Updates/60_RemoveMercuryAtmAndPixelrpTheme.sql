-- Remove the Mercury ATM furni + Builders > Infrastructure page (reverses
-- 40_AddMercuryAtm.sql), and drop the auto-generated Furni > Themes > Pixelrp
-- category. The five Pixelrp-line items (nav teleporters) stay reachable in
-- Builders > Navigation; only their duplicate Furni-tab listing goes away.
-- gen-catalog.py now excludes the 'pixelrp'/'infrastructure' furnilines so the
-- Pixelrp theme never regenerates.
--
-- Idempotent and id-agnostic: pages are matched by (parent, caption) so this is
-- safe on any env (a no-op where the rows never existed, e.g. prod pre-rebuild).

-- 1) Remove placed/owned Mercury ATM instances so deleting the def orphans nothing.
DELETE FROM `items` WHERE `base_item` = 100006;

-- 2) Catalog offer(s) for the ATM.
DELETE FROM `catalog_items` WHERE `item_id` = 100006;

-- 3) Builders > Infrastructure page (was ATM-only).
DELETE ci FROM `catalog_items` ci
    JOIN `catalog_pages` p ON p.`id` = ci.`page_id`
    WHERE p.`parent_id` = 912362 AND p.`caption` = 'Infrastructure';
DELETE FROM `catalog_pages` WHERE `parent_id` = 912362 AND `caption` = 'Infrastructure';

-- 4) Furniture definition.
DELETE FROM `furniture` WHERE `id` = 100006;

-- 5) Furni > Themes > Pixelrp category (generated). Offers first, then the page,
--    matched by caption under the Themes section of the Furni tab (9224).
DELETE ci FROM `catalog_items` ci
    JOIN `catalog_pages` p ON p.`id` = ci.`page_id`
    JOIN `catalog_pages` parent ON parent.`id` = p.`parent_id`
    WHERE p.`caption` = 'Pixelrp' AND parent.`caption` = 'Themes' AND parent.`parent_id` = 9224;
DELETE p FROM `catalog_pages` p
    JOIN `catalog_pages` parent ON parent.`id` = p.`parent_id`
    WHERE p.`caption` = 'Pixelrp' AND parent.`caption` = 'Themes' AND parent.`parent_id` = 9224;

-- Mercury ATM (prp_atm, id 100006) + new Builders > Infrastructure catalog page.
-- Asset bundle lives in nitro/assets/bundled/furniture/prp_atm.nitro with a
-- matching FurnitureData.json entry and extracted catalog icon - those ship
-- with the asset sync, not this file.
--
-- The Infrastructure page is created by (parent, caption) lookup with an
-- auto-increment id: custom page ids 912364-912373 exist on beta as data-only
-- rows that never hit git, so hardcoding the next id is unsafe. Items
-- reference the page via subquery. Icon 47 = generic; icon 207 is the
-- figure-at-cash-machine glyph.
--
-- Idempotent: furniture dedupes on its primary key, the page insert is
-- guarded by NOT EXISTS, and catalog_items clears its own row first.

INSERT IGNORE INTO `furniture`
    (`id`, `item_name`, `public_name`, `type`, `width`, `length`, `stack_height`,
     `can_stack`, `can_sit`, `is_walkable`, `sprite_id`, `allow_recycle`,
     `allow_trade`, `allow_marketplace_sell`, `allow_gift`, `allow_inventory_stack`,
     `interaction_type`, `behaviour_data`, `interaction_modes_count`, `vending_ids`,
     `height_adjustable`, `effect_id`, `wired_id`, `is_rare`, `clothing_id`, `extra_rot`)
VALUES
    (100006, 'prp_atm', 'Mercury ATM', 's', 1, 1, 0, '0', '0', '0', 100006, '0', '1', '0', '1', '1', 'default', 0, 1, '0', '0', 0, 0, '0', 0, '0');

-- visible/enabled are bit(1) columns: write b'1', never '' (see 15_AddNavigationFurni.sql).
INSERT INTO `catalog_pages`
    (`parent_id`, `caption`, `icon_image`, `min_rank`, `min_vip`, `order_num`,
     `page_link`, `page_layout`, `page_strings_1`, `page_strings_2`, `visible`, `enabled`)
SELECT 912362, 'Infrastructure', 207, 2, 0, 4, '', 'default_3x3', '', '', b'1', b'1'
FROM DUAL
WHERE NOT EXISTS (
    SELECT 1 FROM `catalog_pages` WHERE `parent_id` = 912362 AND `caption` = 'Infrastructure'
);

DELETE FROM `catalog_items` WHERE `item_id` = '100006';
INSERT INTO `catalog_items`
    (`page_id`, `item_id`, `catalog_name`, `cost_credits`, `cost_pixels`,
     `cost_diamonds`, `amount`, `offer_active`, `offer_id`)
SELECT p.`id`, '100006', 'prp_atm', 0, 0, 0, 1, '1', -1
FROM `catalog_pages` p
WHERE p.`parent_id` = 912362 AND p.`caption` = 'Infrastructure';

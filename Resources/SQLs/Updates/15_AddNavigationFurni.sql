-- Navigation furni for connecting rooms (Builders > Navigation catalog page).
-- Asset bundles live in nitro/assets/bundled/furniture/ (actionpoint, turfarrow,
-- pharrowtpwhite, tp_arrow, revamp_taxi) with matching FurnitureData.json
-- entries (ids 100001-100005) and extracted catalog icons — those ship with the
-- asset sync, not this file.
--
-- turfarrow / pharrowtpwhite / tp_arrow use interaction_type 'arrow': buying
-- one mints a linked pair (room_items_tele_links), and walking onto an arrow
-- transports the user to its linked twin — including across rooms.

-- The whole script is idempotent: furniture dedupes on its primary key, the
-- page insert is guarded, and catalog_items (no natural unique key) clears its
-- own rows first — safe to re-run after a partial earlier application.
INSERT IGNORE INTO `furniture`
    (`id`, `item_name`, `public_name`, `type`, `width`, `length`, `stack_height`,
     `can_stack`, `can_sit`, `is_walkable`, `sprite_id`, `allow_recycle`,
     `allow_trade`, `allow_marketplace_sell`, `allow_gift`, `allow_inventory_stack`,
     `interaction_type`, `behaviour_data`, `interaction_modes_count`, `vending_ids`,
     `height_adjustable`, `effect_id`, `wired_id`, `is_rare`, `clothing_id`, `extra_rot`)
VALUES
    (100001, 'actionpoint',    'Action Point',           's', 1, 1, 0, '1', '0', '1', 100001, '0', '1', '0', '1', '1', 'default', 0, 5, '0', '0', 0, 0, '0', 0, '0'),
    (100002, 'turfarrow',      'Turf Arrow',             's', 1, 1, 0, '1', '0', '1', 100002, '0', '1', '0', '1', '1', 'arrow',   0, 1, '0', '0', 0, 0, '0', 0, '0'),
    (100003, 'pharrowtpwhite', 'Teleport Arrow (White)', 's', 1, 1, 0, '1', '0', '1', 100003, '0', '1', '0', '1', '1', 'arrow',   0, 1, '0', '0', 0, 0, '0', 0, '0'),
    (100004, 'tp_arrow',       'Teleport Arrow',         's', 1, 1, 0, '1', '0', '1', 100004, '0', '1', '0', '1', '1', 'arrow',   0, 1, '0', '0', 0, 0, '0', 0, '0'),
    (100005, 'revamp_taxi',    'Taxi Sign',              's', 1, 1, 0, '0', '0', '0', 100005, '0', '1', '0', '1', '1', 'default', 0, 2, '0', '0', 0, 0, '0', 0, '0');

-- Builders (912362) exists in the base dump; the Navigation child page does
-- not exist everywhere (prod's migrated catalog lacks it), so create it if
-- missing. visible/enabled are bit(1) columns (the mysql CLI renders them as
-- invisible bytes — don't copy the "empty" look from a SELECT; '' coerces to
-- 0 with strict mode off, which hides/disables the page). The UPDATE repairs
-- rows already created with 0s by an earlier revision of this script.
INSERT IGNORE INTO `catalog_pages`
    (`id`, `parent_id`, `caption`, `icon_image`, `min_rank`, `min_vip`, `order_num`,
     `page_link`, `page_layout`, `page_strings_1`, `page_strings_2`, `visible`, `enabled`)
VALUES
    (912363, 912362, 'Navigation', 47, 2, 0, 3, '', 'default_3x3', '', '', b'1', b'1');
UPDATE `catalog_pages` SET `visible` = b'1', `enabled` = b'1' WHERE `id` = 912363;

DELETE FROM `catalog_items` WHERE `page_id` = 912363 AND `item_id` IN ('100001','100002','100003','100004','100005');
INSERT INTO `catalog_items`
    (`page_id`, `item_id`, `catalog_name`, `cost_credits`, `cost_pixels`,
     `cost_diamonds`, `amount`, `offer_active`, `offer_id`)
VALUES
    (912363, '100001', 'actionpoint',    0, 0, 0, 1, '1', -1),
    (912363, '100002', 'turfarrow',      0, 0, 0, 1, '1', -1),
    (912363, '100003', 'pharrowtpwhite', 0, 0, 0, 1, '1', -1),
    (912363, '100004', 'tp_arrow',       0, 0, 0, 1, '1', -1),
    (912363, '100005', 'revamp_taxi',    0, 0, 0, 1, '1', -1);

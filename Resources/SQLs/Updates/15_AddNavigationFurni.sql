-- Navigation furni for connecting rooms (Builders > Navigation catalog page).
-- Asset bundles live in nitro/assets/bundled/furniture/ (actionpoint, turfarrow,
-- pharrowtpwhite, tp_arrow, revamp_taxi) with matching FurnitureData.json
-- entries (ids 100001-100005) and extracted catalog icons — those ship with the
-- asset sync, not this file.
--
-- turfarrow / pharrowtpwhite / tp_arrow use interaction_type 'arrow': buying
-- one mints a linked pair (room_items_tele_links), and walking onto an arrow
-- transports the user to its linked twin — including across rooms.

INSERT INTO `furniture`
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
-- missing. visible/enabled use the same legacy '' values as the sibling
-- Builders row so the page behaves identically.
INSERT IGNORE INTO `catalog_pages`
    (`id`, `parent_id`, `caption`, `icon_image`, `min_rank`, `min_vip`, `order_num`,
     `page_link`, `page_layout`, `page_strings_1`, `page_strings_2`, `visible`, `enabled`)
VALUES
    (912363, 912362, 'Navigation', 47, 2, 0, 3, '', 'default_3x3', '', '', '', '');

INSERT INTO `catalog_items`
    (`page_id`, `item_id`, `catalog_name`, `cost_credits`, `cost_pixels`,
     `cost_diamonds`, `amount`, `offer_active`, `offer_id`)
VALUES
    (912363, '100001', 'actionpoint',    0, 0, 0, 1, '1', -1),
    (912363, '100002', 'turfarrow',      0, 0, 0, 1, '1', -1),
    (912363, '100003', 'pharrowtpwhite', 0, 0, 0, 1, '1', -1),
    (912363, '100004', 'tp_arrow',       0, 0, 0, 1, '1', -1),
    (912363, '100005', 'revamp_taxi',    0, 0, 0, 1, '1', -1);

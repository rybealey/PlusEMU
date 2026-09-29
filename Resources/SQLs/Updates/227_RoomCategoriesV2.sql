-- pixelrp: the second room-category set. The navigator's Hotel view and the
-- Room tool's Category dropdown offer, in order:
--   Streets, Corporations, Commercial, Residential, Prison, Gang Turf,
--   Heists, Farms, Events, Staff
-- (Most Popular Rooms, id 2, stays above them all at order 1.)
--
-- Every id is reused where it can be: `rooms`.`category` stores the id, so
--   * Industrial (32) BECOMES Streets - every Industrial room moves to Streets
--     with no room row touched;
--   * Corporations (29), Commercial (31), Residential (30) and Staff (34) keep
--     their rooms; Farm (33) is only renamed Farms;
--   * Prison, Gang Turf and Heists take the three stock rows 23_RoomCategories
--     disabled (28 staff_rooms, 35 agencies, 36 all_other_rooms) - nothing is
--     filed under a disabled category, so they start empty;
--   * Events is new (38).
-- Residential (30) stays the fallback for a room whose category can't be
-- honoured (RoomCategories.FallbackId), so it must stay enabled at rank 1.
--
-- Prison and Staff are staff-only to file under (required_rank 5): players
-- don't see them in the room creator, so nobody lists their own room as the
-- prison. Gang Turf is a listing only - it does not make a room a turf.
--
-- Idempotent: every statement sets absolute values.

UPDATE `navigator_categories` SET `category` = 'hotel_view', `category_identifier` = 'streets', `public_name` = 'Streets',
    `view_mode` = 'REGULAR', `required_rank` = 1, `category_type` = 'category', `search_allowance` = 'SHOW_MORE', `enabled` = '1', `order_id` = 2 WHERE `id` = 32;
UPDATE `navigator_categories` SET `order_id` = 3, `enabled` = '1' WHERE `id` = 29;
UPDATE `navigator_categories` SET `order_id` = 4, `enabled` = '1' WHERE `id` = 31;
UPDATE `navigator_categories` SET `order_id` = 5, `enabled` = '1', `required_rank` = 1 WHERE `id` = 30;
UPDATE `navigator_categories` SET `category` = 'hotel_view', `category_identifier` = 'prison', `public_name` = 'Prison',
    `view_mode` = 'REGULAR', `required_rank` = 5, `category_type` = 'category', `search_allowance` = 'SHOW_MORE', `enabled` = '1', `order_id` = 6 WHERE `id` = 28;
UPDATE `navigator_categories` SET `category` = 'hotel_view', `category_identifier` = 'gang_turf', `public_name` = 'Gang Turf',
    `view_mode` = 'REGULAR', `required_rank` = 1, `category_type` = 'category', `search_allowance` = 'SHOW_MORE', `enabled` = '1', `order_id` = 7 WHERE `id` = 35;
UPDATE `navigator_categories` SET `category` = 'hotel_view', `category_identifier` = 'heists', `public_name` = 'Heists',
    `view_mode` = 'REGULAR', `required_rank` = 1, `category_type` = 'category', `search_allowance` = 'SHOW_MORE', `enabled` = '1', `order_id` = 8 WHERE `id` = 36;
UPDATE `navigator_categories` SET `category_identifier` = 'farms', `public_name` = 'Farms', `order_id` = 9, `enabled` = '1' WHERE `id` = 33;

INSERT INTO `navigator_categories` (`id`, `category`, `category_identifier`, `public_name`, `view_mode`, `required_rank`, `category_type`, `search_allowance`, `enabled`, `order_id`)
VALUES (38, 'hotel_view', 'events', 'Events', 'REGULAR', 1, 'category', 'SHOW_MORE', '1', 10)
ON DUPLICATE KEY UPDATE `category` = 'hotel_view', `category_identifier` = 'events', `public_name` = 'Events', `view_mode` = 'REGULAR',
    `required_rank` = 1, `category_type` = 'category', `search_allowance` = 'SHOW_MORE', `enabled` = '1', `order_id` = 10;

UPDATE `navigator_categories` SET `order_id` = 11, `enabled` = '1', `required_rank` = 5 WHERE `id` = 34;

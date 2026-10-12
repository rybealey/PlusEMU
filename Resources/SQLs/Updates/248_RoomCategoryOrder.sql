-- PixelRP: the room categories in the order Twist chose -
--   Corporations, Commercial, Streets,
-- then as 227_RoomCategoriesV2 left them (Residential, Prison, Gang Turf,
-- Heists, Farms, Events, Staff). Most Popular Rooms (id 2, order 1) stays at
-- the top of All Rooms.
--
-- One order for both navigator tabs: All Rooms lists these rows by
-- `order_id`, and the Public tab's PixelRP lists follow the same rows
-- (NavigatorManager.GetPixelRpPublicLists). The Room tool's and the room
-- creator's Category lists follow it too.
--
-- Idempotent: every statement sets an absolute value.
UPDATE `navigator_categories` SET `order_id` = 2 WHERE `id` = 29; -- Corporations
UPDATE `navigator_categories` SET `order_id` = 3 WHERE `id` = 31; -- Commercial
UPDATE `navigator_categories` SET `order_id` = 4 WHERE `id` = 32; -- Streets

SELECT `id`, `public_name`, `order_id` FROM `navigator_categories`
WHERE `category` = 'hotel_view' AND `enabled` = '1' ORDER BY `order_id`;

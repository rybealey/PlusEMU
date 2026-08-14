-- PixelRP room categories replace the stock Habbo set. The navigator's Hotel
-- view and the category dropdown in room settings now offer, in order:
--   Corporations, Residential, Commercial, Industrial, Farm, Staff
-- Staff is rank-gated (>= 5) so only staff can file a room under it, matching
-- the project's staff convention elsewhere.
--
-- Ids 29-34 are reused rather than re-numbered: `rooms`.`category` stores the
-- id, so recycling the enabled rows keeps every foreign reference valid. The
-- three stock categories left over (28 staff_rooms, 35 agencies, 36
-- all_other_rooms) are disabled, not deleted, so they stay recoverable.
--
-- NOT idempotent by design: the final statement parks every existing room in
-- Staff as a one-time move. Re-running it after staff have re-filed rooms
-- would drag them all back to Staff.

UPDATE `navigator_categories` SET `category` = 'hotel_view', `category_identifier` = 'corporations', `public_name` = 'Corporations',
    `view_mode` = 'REGULAR', `required_rank` = 1, `category_type` = 'category', `search_allowance` = 'SHOW_MORE', `enabled` = '1', `order_id` = 2 WHERE `id` = 29;
UPDATE `navigator_categories` SET `category` = 'hotel_view', `category_identifier` = 'residential', `public_name` = 'Residential',
    `view_mode` = 'REGULAR', `required_rank` = 1, `category_type` = 'category', `search_allowance` = 'SHOW_MORE', `enabled` = '1', `order_id` = 3 WHERE `id` = 30;
UPDATE `navigator_categories` SET `category` = 'hotel_view', `category_identifier` = 'commercial', `public_name` = 'Commercial',
    `view_mode` = 'REGULAR', `required_rank` = 1, `category_type` = 'category', `search_allowance` = 'SHOW_MORE', `enabled` = '1', `order_id` = 4 WHERE `id` = 31;
UPDATE `navigator_categories` SET `category` = 'hotel_view', `category_identifier` = 'industrial', `public_name` = 'Industrial',
    `view_mode` = 'REGULAR', `required_rank` = 1, `category_type` = 'category', `search_allowance` = 'SHOW_MORE', `enabled` = '1', `order_id` = 5 WHERE `id` = 32;
UPDATE `navigator_categories` SET `category` = 'hotel_view', `category_identifier` = 'farm', `public_name` = 'Farm',
    `view_mode` = 'REGULAR', `required_rank` = 1, `category_type` = 'category', `search_allowance` = 'SHOW_MORE', `enabled` = '1', `order_id` = 6 WHERE `id` = 33;
UPDATE `navigator_categories` SET `category` = 'hotel_view', `category_identifier` = 'staff', `public_name` = 'Staff',
    `view_mode` = 'REGULAR', `required_rank` = 5, `category_type` = 'category', `search_allowance` = 'SHOW_MORE', `enabled` = '1', `order_id` = 7 WHERE `id` = 34;

-- Retired stock categories.
UPDATE `navigator_categories` SET `enabled` = '0' WHERE `id` IN (28, 35, 36);

-- One-time move: every room that exists today goes to Staff, for staff to
-- re-file deliberately.
UPDATE `rooms` SET `category` = 34;

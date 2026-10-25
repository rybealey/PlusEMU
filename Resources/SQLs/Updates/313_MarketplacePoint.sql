-- pixelrp: the Marketplace point (2026-10-26). The Armoury's action point -
-- item 11562, room 55 - gets the `marketplace` behaviour on its own: step on
-- it and the Marketplace opens, step off and it closes. Every other action
-- point stays as it is.
--
-- The same row the Furni function tool writes for a single-item change
-- (ItemFunctionOverrides). Read when the room loads, which the restart after
-- the deploy does.
--
-- Only while 11562 is still a PixelRP Action Point (base item 100001), so a
-- database where that id is something else is left alone. Idempotent.

INSERT INTO `rp_item_function` (`item_id`, `field`, `value`, `set_by`, `created_at`)
SELECT `id`, 'interaction_type', 'marketplace', 0, UNIX_TIMESTAMP()
FROM `items` WHERE `id` = 11562 AND `base_item` = 100001
ON DUPLICATE KEY UPDATE `value` = 'marketplace';

-- Should list one row: 11562 / interaction_type / marketplace.
SELECT `item_id`, `field`, `value` FROM `rp_item_function` WHERE `item_id` = 11562;

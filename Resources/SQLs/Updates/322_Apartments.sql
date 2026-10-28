-- pixelrp: Apartments (2026-10-28). Any player can buy one apartment - a room
-- of their own - for $5,000 cash at an Apartments point (RpApartments).
--
-- An apartment is drawn on a 5x5 grid of lots; the first one bought is the
-- centre lot (index 12, counted left to right, top to bottom), and more lots
-- that share a side with it can be bought to make it bigger. `lots` lists the
-- lots it covers, `paid` everything paid for them (selling back returns half).
-- `lobby_room_id` is the room it was bought in, where its exit arrow leads
-- back to. `list_price` is its asking price on the real estate board, 0 while
-- it is not for sale.

CREATE TABLE IF NOT EXISTS `rp_apartments` (
  `user_id` INT NOT NULL,
  `room_id` INT UNSIGNED NOT NULL,
  `lots` VARCHAR(100) NOT NULL DEFAULT '12',
  `lobby_room_id` INT UNSIGNED NOT NULL DEFAULT 0,
  `paid` INT NOT NULL DEFAULT 0,
  `bought_at` INT NOT NULL DEFAULT 0,
  `list_price` INT NOT NULL DEFAULT 0,
  `listed_at` INT NOT NULL DEFAULT 0,
  PRIMARY KEY (`user_id`),
  UNIQUE KEY `room_id` (`room_id`)
) ENGINE=InnoDB DEFAULT CHARSET=latin1;

-- The SF Apartments point: the action point in room 67, "SF Apartments
-- [Interior]" - item 11561 - gets the `apartments` behaviour on its own. Step
-- on it and the Apartments window opens, step off and it closes. The same row
-- the Furni function tool writes for a single-item change, read when the room
-- loads. Only while 11561 is still a PixelRP Action Point (base item 100001).
-- Idempotent.

INSERT INTO `rp_item_function` (`item_id`, `field`, `value`, `set_by`, `created_at`)
SELECT `id`, 'interaction_type', 'apartments', 0, UNIX_TIMESTAMP()
FROM `items` WHERE `id` = 11561 AND `base_item` = 100001
ON DUPLICATE KEY UPDATE `value` = 'apartments';

-- Should list one row: 11561 / interaction_type / apartments.
SELECT `item_id`, `field`, `value` FROM `rp_item_function` WHERE `item_id` = 11561;

-- The Real Estate point: SF Apartments' second action point - item 24928, also
-- in room 67 - gets the `real_estate` behaviour: step on it and the real estate
-- board opens (RpRealEstate). Same guard. Idempotent.

INSERT INTO `rp_item_function` (`item_id`, `field`, `value`, `set_by`, `created_at`)
SELECT `id`, 'interaction_type', 'real_estate', 0, UNIX_TIMESTAMP()
FROM `items` WHERE `id` = 24928 AND `base_item` = 100001
ON DUPLICATE KEY UPDATE `value` = 'real_estate';

-- Should list one row: 24928 / interaction_type / real_estate.
SELECT `item_id`, `field`, `value` FROM `rp_item_function` WHERE `item_id` = 24928;

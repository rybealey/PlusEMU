-- pixelrp: the bank deposit box (HabboHotel/Users/Banking/DepositBox).
--
-- Every player with a bank account has one: backpack items stored at the bank,
-- out of reach until they come back for them. Opened by stepping onto furni
-- with the `deposit_box` behaviour (set in the Furni function editor).
--
-- The same shape as user_rp_inventory, so an item moves between the two as a
-- row: slots 1-16 open to everybody, 17-20 while VIP. A row's `count` is how
-- many of the item sit in that slot (stacks stop at 10, weapons never stack -
-- the backpack's rules, applied on both sides).

CREATE TABLE IF NOT EXISTS `user_rp_deposit_box` (
  `user_id` int NOT NULL,
  `slot` int NOT NULL,
  `item` varchar(64) NOT NULL,
  `count` int NOT NULL DEFAULT 1,
  PRIMARY KEY (`user_id`, `slot`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

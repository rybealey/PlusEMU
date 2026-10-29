-- pixelrp: deposit box slots are bought (DepositBox.BuySlot, 2026-10-29).
--
-- Everybody's box starts with 4 open slots; each one after costs $350, up to
-- all 24, and VIP no longer opens any. `slots` is how many a player has open -
-- no row means the first 4. Items already stored past a player's open slots
-- stay where they are and can be taken out, but nothing new goes in there.

CREATE TABLE IF NOT EXISTS `user_rp_deposit_slots` (
  `user_id` int NOT NULL,
  `slots` int NOT NULL DEFAULT 4,
  PRIMARY KEY (`user_id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

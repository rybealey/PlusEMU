-- pixelrp: the coin ledger (HabboHotel/Users/Banking/CoinLedger) - every
-- change to a player's coins on hand, with its source and the balance it left.
-- The bank keeps its own ledger (rp_bank_transactions, migration 110); the
-- City Panel's Global Ledger reads both, newest first.
--
-- Starts empty: coins that moved before this shipped were never recorded.

CREATE TABLE IF NOT EXISTS `rp_coin_ledger` (
  `id` int NOT NULL AUTO_INCREMENT,
  `user_id` int NOT NULL,
  `username` varchar(32) NOT NULL DEFAULT '',
  `amount` int NOT NULL,
  `balance_after` int NOT NULL DEFAULT 0,
  `source` varchar(96) NOT NULL DEFAULT '',
  `created_at` int NOT NULL,
  PRIMARY KEY (`id`),
  KEY `user` (`user_id`, `id`),
  KEY `created` (`created_at`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

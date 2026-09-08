-- pixelrp: the Stocks app.
--
-- rp_corporations.stock is INVENTORY - how much raw material a corporation is
-- holding - so the app needs to know what "full" looks like before a number
-- means anything to a farmer. stock_capacity is that ceiling.
--
-- History is not kept anywhere today, so the ledger below records a sample per
-- corporation every 15 minutes and keeps 90 days of them; StockLedger purges
-- anything older on its own tick.

ALTER TABLE `rp_corporations`
  ADD COLUMN `stock_capacity` INT NOT NULL DEFAULT 0 AFTER `stock`;

-- A capacity of 0 would make "how full" undefined, so give every corporation a
-- starting ceiling. Tune per corporation from here.
UPDATE `rp_corporations` SET `stock_capacity` = 1000 WHERE `stock_capacity` = 0;

CREATE TABLE IF NOT EXISTS `rp_corporation_stock_samples` (
  `id` INT NOT NULL AUTO_INCREMENT,
  `corporation_id` INT NOT NULL,
  `value` INT NOT NULL,
  `sampled_at` INT NOT NULL,
  PRIMARY KEY (`id`),
  KEY `corp_time` (`corporation_id`, `sampled_at`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

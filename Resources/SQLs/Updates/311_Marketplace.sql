-- pixelrp: the Marketplace (2026-10-25). Players sell backpack items to each
-- other through :mp (RpMarket.cs).
--
-- rp_market_listings: one row per listing. A listed item has LEFT the backpack;
-- it lives here until it sells or its seller takes it back. `left_qty` is what
-- is still for sale, `sold_qty` what has sold; `ended_at` is 0 while the
-- listing is up, else when it sold out or was taken back. `wear` is a weapon's
-- or armour's wear (RpDurability), kept on the way in and out.
--
-- rp_market_sales: one row per item sold, for the record. The window never
-- shows who bought or sold.
--
-- command_mp: every player - group 1, a MINIMUM rank. Takes effect when the
-- emulator restarts, which the deploy does next.
--
-- Idempotent.

CREATE TABLE IF NOT EXISTS `rp_market_listings` (
  `id` INT NOT NULL AUTO_INCREMENT,
  `seller_id` INT NOT NULL,
  `seller_name` VARCHAR(50) NOT NULL DEFAULT '',
  `item` VARCHAR(64) NOT NULL,
  `wear` INT NOT NULL DEFAULT 0,
  `price` INT NOT NULL,
  `listed_qty` INT NOT NULL,
  `left_qty` INT NOT NULL,
  `sold_qty` INT NOT NULL DEFAULT 0,
  `listed_at` INT NOT NULL,
  `ended_at` INT NOT NULL DEFAULT 0,
  PRIMARY KEY (`id`),
  KEY `live` (`ended_at`, `left_qty`),
  KEY `seller` (`seller_id`, `ended_at`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

CREATE TABLE IF NOT EXISTS `rp_market_sales` (
  `id` INT NOT NULL AUTO_INCREMENT,
  `listing_id` INT NOT NULL,
  `seller_id` INT NOT NULL,
  `buyer_id` INT NOT NULL,
  `buyer_name` VARCHAR(50) NOT NULL DEFAULT '',
  `item` VARCHAR(64) NOT NULL,
  `wear` INT NOT NULL DEFAULT 0,
  `price` INT NOT NULL,
  `sold_at` INT NOT NULL,
  PRIMARY KEY (`id`),
  KEY `seller` (`seller_id`, `sold_at`),
  KEY `buyer` (`buyer_id`, `sold_at`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

INSERT INTO `permissions_commands` (`command`, `group_id`, `subscription_id`)
VALUES ('command_mp', 1, 0)
ON DUPLICATE KEY UPDATE `group_id` = VALUES(`group_id`), `subscription_id` = VALUES(`subscription_id`);

-- Should list one row at 1 / 0.
SELECT `command`, `group_id`, `subscription_id` FROM `permissions_commands`
WHERE `command` = 'command_mp';

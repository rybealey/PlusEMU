-- pixelrp: the City Panel's Economy tab (HabboHotel/CityPanel/CityEconomy).
--
-- rp_service_prices: what each corporation's services will cost. Every
-- corporation is to bill - the hospital for medkits and heals, The Muse for
-- energy, snacks and the passive drink - and staff set the prices here. A
-- price is stored and edited now; each service reads it (ServicePrices.Get)
-- when its own billing ships. Until then they cost nothing, as today, which
-- is also why every seed price is 0.
--
-- corporation_id is who is paid. The hospital is found by its service type,
-- The Muse by name (it was founded in game, not by a migration); either left
-- at 0 when there is none yet, and set later from the panel's own data.
--
--   rp_city_economy  editing pay per rank and service prices - rank 8, money.
--   rp_city_shifts   clocking somebody out from the panel - rank 5.
--
-- Idempotent.

CREATE TABLE IF NOT EXISTS `rp_service_prices` (
  `key` varchar(32) NOT NULL,
  `corporation_id` int NOT NULL DEFAULT 0,
  `name` varchar(64) NOT NULL,
  `price` int NOT NULL DEFAULT 0,
  `sort_order` int NOT NULL DEFAULT 0,
  `updated_by` int NOT NULL DEFAULT 0,
  `updated_at` int NOT NULL DEFAULT 0,
  PRIMARY KEY (`key`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

INSERT IGNORE INTO `rp_service_prices` (`key`, `corporation_id`, `name`, `price`, `sort_order`)
SELECT 'medkit', COALESCE((SELECT `id` FROM `rp_corporations` WHERE `service_type` = 'medical' ORDER BY `id` LIMIT 1), 0), 'Medkit', 0, 1;

INSERT IGNORE INTO `rp_service_prices` (`key`, `corporation_id`, `name`, `price`, `sort_order`)
SELECT 'heal', COALESCE((SELECT `id` FROM `rp_corporations` WHERE `service_type` = 'medical' ORDER BY `id` LIMIT 1), 0), 'Heal at the hospital', 0, 2;

INSERT IGNORE INTO `rp_service_prices` (`key`, `corporation_id`, `name`, `price`, `sort_order`)
SELECT 'energy', COALESCE((SELECT `id` FROM `rp_corporations` WHERE `name` LIKE '%Muse%' ORDER BY `id` LIMIT 1), 0), 'Energy', 0, 3;

INSERT IGNORE INTO `rp_service_prices` (`key`, `corporation_id`, `name`, `price`, `sort_order`)
SELECT 'snack', COALESCE((SELECT `id` FROM `rp_corporations` WHERE `name` LIKE '%Muse%' ORDER BY `id` LIMIT 1), 0), 'Snack', 0, 4;

INSERT IGNORE INTO `rp_service_prices` (`key`, `corporation_id`, `name`, `price`, `sort_order`)
SELECT 'passive_drink', COALESCE((SELECT `id` FROM `rp_corporations` WHERE `name` LIKE '%Muse%' ORDER BY `id` LIMIT 1), 0), 'Passive drink', 0, 5;

INSERT INTO `permissions_commands` (`command`, `group_id`, `subscription_id`)
VALUES ('rp_city_economy', 8, 0)
ON DUPLICATE KEY UPDATE `group_id` = 8, `subscription_id` = 0;

INSERT INTO `permissions_commands` (`command`, `group_id`, `subscription_id`)
VALUES ('rp_city_shifts', 5, 0)
ON DUPLICATE KEY UPDATE `group_id` = 5, `subscription_id` = 0;

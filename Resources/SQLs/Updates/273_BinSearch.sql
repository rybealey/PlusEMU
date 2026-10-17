-- PixelRP: searching bins (Twist, 2026-10-18).
--
-- The PixelRP Bin (anubis_bin, 161) becomes a bin to search: in a
-- PixelRP-owned room, a double-click from beside it is ten seconds of
-- rummaging with flies overhead, then one find, and the bin stays open -
-- searched - for fifteen minutes (HabboHotel/Rooms/BinSearch). Anywhere else
-- it opens and shuts as before. bin_search is a behaviour, so the Function
-- tool can make any furni with an open state one.
--
-- rp_bin_rewards: what a search can turn up, set in the City Panel's Economy
-- tab, Bin Search (BinRewards). A search picks one row, each row's chance
-- being its weight over the total, and pays between min_amount and
-- max_amount of it. `reward` is a backpack item key, 'coins' or 'nothing'.
-- Seeded with Twist's list - a Medkit, a Snack, a Passive Smoothie, 3-20c -
-- and a chance of nothing, all even to start with.
--
-- Editing the rewards is the Economy permission (rp_city_economy, 235): they
-- are money.
--
-- Every bin is shut to begin with, so none starts the feature looking
-- searched. Idempotent: the seed only fills an empty table.

UPDATE `furniture` SET `interaction_type` = 'bin_search', `interaction_modes_count` = 2
 WHERE `id` = 110500 AND `item_name` = 'anubis_bin';

UPDATE `items` SET `extra_data` = '0' WHERE `base_item` = 110500 AND `extra_data` <> '0';

CREATE TABLE IF NOT EXISTS `rp_bin_rewards` (
  `id` int NOT NULL AUTO_INCREMENT,
  `reward` varchar(32) NOT NULL,
  `weight` int NOT NULL DEFAULT 0,
  `min_amount` int NOT NULL DEFAULT 1,
  `max_amount` int NOT NULL DEFAULT 1,
  `sort_order` int NOT NULL DEFAULT 0,
  `updated_by` int NOT NULL DEFAULT 0,
  `updated_at` int NOT NULL DEFAULT 0,
  PRIMARY KEY (`id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

INSERT INTO `rp_bin_rewards` (`reward`, `weight`, `min_amount`, `max_amount`, `sort_order`)
SELECT seed.`reward`, seed.`weight`, seed.`min_amount`, seed.`max_amount`, seed.`sort_order`
  FROM (
      SELECT 'medkit' AS `reward`, 20 AS `weight`, 1 AS `min_amount`, 1 AS `max_amount`, 1 AS `sort_order`
      UNION ALL SELECT 'snack', 20, 1, 1, 2
      UNION ALL SELECT 'smoothie', 20, 1, 1, 3
      UNION ALL SELECT 'coins', 20, 3, 20, 4
      UNION ALL SELECT 'nothing', 20, 0, 0, 5
  ) AS seed
 WHERE NOT EXISTS (SELECT 1 FROM `rp_bin_rewards`);

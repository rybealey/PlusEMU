-- pixelrp: uniforms (HabboHotel/Corporations/UniformManager), set in the City
-- Panel's Uniforms tab.
--
-- One outfit per wearer and gender:
--   kind 'rank'      a corporation rank's uniform, worn while clocked in at
--                    that rank (rank_id -> rp_corporation_ranks.id).
--   kind 'prisoner'  worn by everyone serving a jail sentence (rank_id 0).
--
-- `figure` holds only clothing - hat, head and face accessories, eyewear,
-- shirt, jacket, print, chest accessory, trousers, shoes, belt. Hair and face
-- are never part of it: the uniform dresses the player, it does not change
-- who they are. A missing row is "no uniform": the player wears their own.
--
-- The uniform is only ever applied in memory; users.look keeps the player's
-- own look, so a relog or crash always gives it back.
--
-- rp_city_uniforms: who may edit them from the panel (rank 5, like the rest
-- of it; a threshold - see 135).

CREATE TABLE IF NOT EXISTS `rp_uniforms` (
  `id` int NOT NULL AUTO_INCREMENT,
  `kind` enum('rank','prisoner') NOT NULL,
  `rank_id` int NOT NULL DEFAULT 0,
  `gender` enum('M','F') NOT NULL,
  `figure` varchar(512) NOT NULL DEFAULT '',
  `updated_by` int NOT NULL DEFAULT 0,
  `updated_at` int NOT NULL DEFAULT 0,
  PRIMARY KEY (`id`),
  UNIQUE KEY `uniq_uniform` (`kind`, `rank_id`, `gender`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

INSERT INTO `permissions_commands` (`command`, `group_id`, `subscription_id`)
VALUES ('rp_city_uniforms', 5, 0)
ON DUPLICATE KEY UPDATE `group_id` = 5, `subscription_id` = 0;

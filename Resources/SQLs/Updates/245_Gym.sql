-- PixelRP: the gym - Strength and Stamina (GymState).
--
-- Two stats, levels 0-10 each, trained by standing on gym equipment with a
-- membership bought from a Gym bot (30c for an hour of ONLINE time, stackable).
-- Stamina is health: max health is 100 + 10 per Stamina level. Strength is
-- :hit damage. Stamina drops one level after 30 hours online without a
-- treadmill workout, once, and the level has to be trained again.
--
-- Five parts:
--   1. user_rp_stats grows the gym's columns (and its own online clock - see
--      below).
--   2. Both ai_type enums take 'gym', so a bot can BE a Gym bot.
--   3. The Gym Trainer bot in the shop, beside the Bank Teller.
--   4. Every Gym Treadmill trains Stamina and every Hi-tech Crosstrainer
--      Strength - set on the furni type, so every copy in the hotel is one.
--   5. :setgym for staff.
--
-- Idempotent: guarded ALTERs, fixed ids cleared before insert, absolute
-- UPDATEs.

-- 1. The stats row. No migration in this repo ever created user_rp_stats (it
--    predates them), so it is declared here as the emulator reads it - a no-op
--    wherever it already exists, and the guarded ALTER below cannot fail on a
--    database that never had it.
CREATE TABLE IF NOT EXISTS `user_rp_stats` (
  `user_id` int NOT NULL,
  `health` int NOT NULL DEFAULT 100,
  `health_max` int NOT NULL DEFAULT 100,
  `energy` int NOT NULL DEFAULT 100,
  `energy_max` int NOT NULL DEFAULT 100,
  `passive_seconds` int NOT NULL DEFAULT 0,
  PRIMARY KEY (`user_id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

-- `online_seconds` is the gym's OWN clock of time spent logged in. The hotel's
-- user_statistics.onlineTime cannot be used: Habbo.SessionStart is never set,
-- so every logout adds a whole unix timestamp to it. Membership and the
-- Stamina clock are both readings of this one: `gym_until` is the reading at
-- which the membership ends, `stamina_trained_at` the reading at the last
-- treadmill workout. `*_seconds` is training toward the NEXT level.
SET @col := (SELECT COUNT(*) FROM information_schema.COLUMNS
  WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'user_rp_stats' AND COLUMN_NAME = 'strength_level');
SET @sql := IF(@col = 0,
  'ALTER TABLE `user_rp_stats`
     ADD COLUMN `strength_level` int NOT NULL DEFAULT 0,
     ADD COLUMN `strength_seconds` int NOT NULL DEFAULT 0,
     ADD COLUMN `stamina_level` int NOT NULL DEFAULT 0,
     ADD COLUMN `stamina_seconds` int NOT NULL DEFAULT 0,
     ADD COLUMN `stamina_trained_at` int NOT NULL DEFAULT 0,
     ADD COLUMN `stamina_dropped` tinyint NOT NULL DEFAULT 0,
     ADD COLUMN `gym_until` int NOT NULL DEFAULT 0,
     ADD COLUMN `online_seconds` int NOT NULL DEFAULT 0',
  'SELECT 1');
PREPARE s FROM @sql; EXECUTE s; DEALLOCATE PREPARE s;

-- Everybody starts at Stamina 0, which is 100 health. Nothing ever wrote
-- health_max, so this should touch nothing - the emulator keeps it in step
-- with the level from here on.
UPDATE `user_rp_stats` SET `health_max` = 100, `health` = LEAST(`health`, 100) WHERE `stamina_level` = 0 AND `health_max` <> 100;

-- 2. BOTH enums, in one go - 120 widened `bots` and missed the preset column,
--    and outside strict mode (compose.yaml) the invalid value was silently
--    stored as '' (121). 'gym' goes last so the existing values keep their
--    ordinals.
ALTER TABLE `bots`
    MODIFY COLUMN `ai_type` enum('generic','bartender','pet','banker','gym')
    NOT NULL DEFAULT 'generic';

ALTER TABLE `catalog_bot_presets`
    MODIFY COLUMN `ai_type` enum('pet','generic','bartender','banker','gym')
    NOT NULL DEFAULT 'generic';

-- 3. The Gym Trainer: a catalog bot is three rows (see 115). 107501 is next
--    to the Bank Teller (107500). The figure is the teller's, verbatim - every
--    part id in it is known to render, and a bot can be re-dressed in game.
DELETE FROM `catalog_items` WHERE `item_id` = '107501';
DELETE FROM `catalog_bot_presets` WHERE `id` = 107501;
DELETE FROM `furniture` WHERE `id` = 107501;

INSERT INTO `furniture`
    (`id`,`item_name`,`public_name`,`type`,`width`,`length`,`stack_height`,`can_stack`,`can_sit`,
     `is_walkable`,`sprite_id`,`allow_recycle`,`allow_trade`,`allow_marketplace_sell`,`allow_gift`,
     `allow_inventory_stack`,`interaction_type`,`behaviour_data`,`interaction_modes_count`,`is_rare`)
VALUES
    (107501,'bot_gym','Gym Trainer','r',1,1,0.0,'0','0','0',0,'1','1','1','0','1','default',0,1,'0');

INSERT INTO `catalog_bot_presets` (`id`,`name`,`figure`,`gender`,`motto`,`ai_type`)
VALUES
    (107501,'Gym Trainer','hr-3020-34.hd-3091-2.ch-225-92.lg-3058-100.sh-3089-1338.ca-3084-78-108.wa-2005',
     'M','Ready to train?','gym');

-- On whatever page the Bank Teller is on now, else beside bot_generic, else
-- the seeded Bots page.
SET @botpage := COALESCE(
    (SELECT `page_id` FROM `catalog_items` WHERE `item_id` = '107500' LIMIT 1),
    (SELECT `page_id` FROM `catalog_items` WHERE `item_id` = '3569' LIMIT 1),
    9);

INSERT INTO `catalog_items`
    (`page_id`,`item_id`,`catalog_name`,`cost_credits`,`cost_pixels`,`cost_diamonds`,
     `amount`,`limited_sells`,`limited_stack`,`offer_active`,`extradata`,`badge`,`offer_id`)
VALUES
    (@botpage, '107501', 'Gym Trainer', 0, 0, 0, 1, 0, 0, '1', '', '', -1);

-- 4. The machines. On the furni TYPE, so every treadmill and crosstrainer in
--    the hotel trains - Twist's call. Staff can put either behaviour on other
--    furni with the Function tool.
UPDATE `furniture` SET `interaction_type` = 'gym_stamina' WHERE `item_name` = 'olympics_c16_treadmill';
UPDATE `furniture` SET `interaction_type` = 'gym_strength' WHERE `item_name` = 'olympics_c16_crosstrainer';

-- 5. :setgym <username> <strength|stamina> <level> - staff, group 5 like the
--    other staff commands.
INSERT INTO `permissions_commands` (`command`, `group_id`, `subscription_id`)
VALUES ('command_setgym', 5, 0)
ON DUPLICATE KEY UPDATE `group_id` = 5;

-- Should read 'gym' twice, then the two machine behaviours.
SELECT `id`, `name`, `ai_type` FROM `catalog_bot_presets` WHERE `id` = 107501;
SELECT `item_name`, `interaction_type` FROM `furniture`
    WHERE `item_name` IN ('olympics_c16_treadmill', 'olympics_c16_crosstrainer');

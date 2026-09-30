-- pixelrp: the City Panel - the staff window opened from Mod Tools
-- (HabboHotel/CityPanel).
--
-- In permissions_commands, group_id is a rank THRESHOLD: a player holds every
-- command where `rank >= group_id` (see 135). Three new ones:
--
--   rp_city_panel    opening the panel at all: every staff member, rank 5.
--   rp_city_justice  releasing a prisoner and clearing a charge sheet from it.
--                    Rank 5 - staff already :summon prisoners out.
--   rp_city_balance  moving a player's coins. Rank 8: money is the one thing
--                    here that cannot be put back by looking at the room.
--
-- Everything else the panel does is gated by the command it mirrors -
-- command_restore, command_kill, command_summon, command_goto, command_spawn -
-- so changing one of those rows changes the panel too.
--
-- Idempotent: absolute assignments.

INSERT INTO `permissions_commands` (`command`, `group_id`, `subscription_id`)
VALUES ('rp_city_panel', 5, 0)
ON DUPLICATE KEY UPDATE `group_id` = 5, `subscription_id` = 0;

INSERT INTO `permissions_commands` (`command`, `group_id`, `subscription_id`)
VALUES ('rp_city_justice', 5, 0)
ON DUPLICATE KEY UPDATE `group_id` = 5, `subscription_id` = 0;

INSERT INTO `permissions_commands` (`command`, `group_id`, `subscription_id`)
VALUES ('rp_city_balance', 8, 0)
ON DUPLICATE KEY UPDATE `group_id` = 8, `subscription_id` = 0;

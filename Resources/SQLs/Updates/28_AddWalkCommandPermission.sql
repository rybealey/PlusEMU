-- PixelRP :walk command (staff): forces a player to patrol one axis until
-- they click a tile of their own. Group 3 matches command_summon/command_goto.
INSERT IGNORE INTO `permissions_commands` (`command`, `group_id`, `subscription_id`) VALUES ('command_walk', 3, 0);

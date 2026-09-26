-- pixelrp: :spit, a social command with a splat. group_id 1 = every player,
-- matching :slap and :push - it is a gesture, not a moderator tool, and in a
-- safe zone it costs nobody anything.
--
-- `group_id` is a RANK THRESHOLD here, not a group: GetCommandsForPlayer tests
-- player.Rank >= group_id, so 1 is everybody.
--
-- The furni it throws (xmas13_paintsplat2, "Blue Paint Splat") needs no row of
-- its own: the command builds the splat in memory and sends it straight to the
-- room, so it is never written to `items` and never owned.
--
-- Idempotent.
INSERT INTO `permissions_commands` (`command`, `group_id`, `subscription_id`)
VALUES ('command_spit', 1, 0)
ON DUPLICATE KEY UPDATE `group_id` = 1;

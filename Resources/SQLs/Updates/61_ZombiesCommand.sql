-- pixelrp stress testing: :zombies spawns freeroaming NPC clones of a player
-- and bare :zombies removes them. Staff-only (group 5), matching the other
-- staff commands. Idempotent.
INSERT INTO `permissions_commands` (`command`, `group_id`, `subscription_id`)
VALUES ('command_zombies', 5, 0)
ON DUPLICATE KEY UPDATE `group_id` = 5;

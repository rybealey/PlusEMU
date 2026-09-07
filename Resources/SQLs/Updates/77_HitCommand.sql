-- pixelrp fighting system: :hit. group_id 1 = every player, matching :slap
-- and :push - fighting is not a staff perk. Idempotent.
INSERT INTO `permissions_commands` (`command`, `group_id`, `subscription_id`)
VALUES ('command_hit', 1, 0)
ON DUPLICATE KEY UPDATE `group_id` = 1;

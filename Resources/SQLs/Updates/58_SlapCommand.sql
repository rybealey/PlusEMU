-- pixelrp fighting system: :slap, the first combat action. group_id 1 = every
-- player, matching :push (command_push is also group 1). Idempotent.
INSERT INTO `permissions_commands` (`command`, `group_id`, `subscription_id`)
VALUES ('command_slap', 1, 0)
ON DUPLICATE KEY UPDATE `group_id` = 1;

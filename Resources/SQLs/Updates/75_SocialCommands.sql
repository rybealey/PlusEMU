-- pixelrp social commands: :hug, :kiss and :bite. group_id 1 = every player,
-- matching :slap and :push. Idempotent.
INSERT INTO `permissions_commands` (`command`, `group_id`, `subscription_id`)
VALUES ('command_hug', 1, 0), ('command_kiss', 1, 0), ('command_bite', 1, 0)
ON DUPLICATE KEY UPDATE `group_id` = 1;

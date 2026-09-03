-- pixelrp: :passive, the chat front door for the Passive Smoothie. Toggles
-- passive status on (consuming a smoothie from the backpack, with the same
-- safe-zone and full-health gates the Backpack item applies) or off. group_id 1
-- = every player, matching :push and :slap. Idempotent.
INSERT INTO `permissions_commands` (`command`, `group_id`, `subscription_id`)
VALUES ('command_passive', 1, 0)
ON DUPLICATE KEY UPDATE `group_id` = 1;

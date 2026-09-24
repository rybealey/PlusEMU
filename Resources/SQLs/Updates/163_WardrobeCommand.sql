-- pixelrp: staff-only :wd opens the clothes editor anywhere (rank 5+ =
-- group_id 5, the same floor as the other staff commands). Idempotent.
INSERT INTO `permissions_commands` (`command`, `group_id`, `subscription_id`)
VALUES ('command_wd', 5, 0)
ON DUPLICATE KEY UPDATE `group_id` = 5;

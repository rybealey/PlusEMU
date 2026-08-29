-- Staff-only :superfire grant (rank 5+, same floor as :superhire).
INSERT INTO `permissions_commands` (`command`, `group_id`, `subscription_id`)
VALUES ('command_superfire', 5, 0)
ON DUPLICATE KEY UPDATE `group_id` = 5;

-- pixelrp: staff-only :roomsettings and :floorplan (rank 5+ = group_id 5,
-- same grant floor as :superhire/:superfire). Idempotent.
INSERT INTO `permissions_commands` (`command`, `group_id`, `subscription_id`)
VALUES ('command_roomsettings', 5, 0)
ON DUPLICATE KEY UPDATE `group_id` = 5;
INSERT INTO `permissions_commands` (`command`, `group_id`, `subscription_id`)
VALUES ('command_floorplan', 5, 0)
ON DUPLICATE KEY UPDATE `group_id` = 5;

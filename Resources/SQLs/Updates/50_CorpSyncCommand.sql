-- :corpsync [corp_key] - staff command (rank 5+, same floor as superhire)
-- that re-broadcasts every employee's employment hotel-wide after corp-level
-- DB edits (badge, name, acronym, rank names) so infostands, profiles and
-- corp windows update in real-time without relogs.

INSERT INTO `permissions_commands` (`command`, `group_id`, `subscription_id`)
VALUES ('command_corpsync', 5, 0)
ON DUPLICATE KEY UPDATE `group_id` = 5;

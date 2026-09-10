-- pixelrp: :pardon <player> - drop every open charge against someone.
--
-- The other half of :charge. Like the rest of the police chain the permission
-- is open to everyone and the command itself checks the job: PoliceUtility
-- requires an on-duty employee of a corporation flagged `is_police`, so the
-- gate lives in one place rather than being split between here and the code.
INSERT INTO `permissions_commands` (`command`, `group_id`, `subscription_id`)
VALUES ('command_pardon', 1, 0)
ON DUPLICATE KEY UPDATE `group_id` = 1;

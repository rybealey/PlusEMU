-- pixelrp: :ra (room alert) for staff, rank 5 - and with it the City Panel's
-- "This room" alert, which follows the same permission.
--
-- The stock row was group 1 with subscription_id 2. A command needs BOTH
-- rank >= group_id and VipRank >= subscription_id
-- (PermissionManager.GetCommandsForPlayer), and VipRank here is only ever 0 or
-- 1 (Habbo.VipRank: VIP or not). So nobody, staff included, could ever use
-- it. Rank 5 and no subscription, like :sa (172) - it is a moderator tool.
--
-- Idempotent: an absolute assignment.

INSERT INTO `permissions_commands` (`command`, `group_id`, `subscription_id`)
VALUES ('command_room_alert', 5, 0)
ON DUPLICATE KEY UPDATE `group_id` = 5, `subscription_id` = 0;

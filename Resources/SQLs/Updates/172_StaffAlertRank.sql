-- pixelrp: :sa is for rank 5 and up. It was rank 2 in the base dump, from
-- when it was a popup; it is now a staff chat line (StaffAlertCommand, bubble
-- 201) and only reaches rank 5+, so only rank 5+ can send it too.
--
-- `group_id` is a RANK THRESHOLD, not a group. Takes effect for a player at
-- their next login, when their commands are rebuilt.
--
-- Idempotent.
INSERT INTO `permissions_commands` (`command`, `group_id`, `subscription_id`)
VALUES ('command_staff_alert', 5, 0)
ON DUPLICATE KEY UPDATE `group_id` = 5;

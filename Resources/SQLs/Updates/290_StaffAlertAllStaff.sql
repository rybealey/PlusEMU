-- pixelrp: :sa opens to every staff rank (Twist, 2026-10-21: "all staff get
-- access to :sa"). It was Administrator and up (172); StaffAlertCommand now
-- sends it to Builder and up too, and the Chat History Staff tab shows from
-- Builder.
--
-- group_id is a MINIMUM rank: 2 = Builder and every rank above it. Takes
-- effect when the emulator restarts, which the deploy does next.
--
-- Idempotent: an upsert.

INSERT INTO `permissions_commands` (`command`, `group_id`, `subscription_id`)
VALUES ('command_staff_alert', 2, 0)
ON DUPLICATE KEY UPDATE `group_id` = VALUES(`group_id`), `subscription_id` = VALUES(`subscription_id`);

-- Should list one row at 2 / 0.
SELECT `command`, `group_id`, `subscription_id` FROM `permissions_commands`
WHERE `command` = 'command_staff_alert';

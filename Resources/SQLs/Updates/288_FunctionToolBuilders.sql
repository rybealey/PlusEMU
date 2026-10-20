-- pixelrp: the Function tool opens to Builders (Twist, 2026-10-21: "builders
-- have access to function tool with no limits"). It was Administrator and up
-- (135). The client no longer holds the button back for anyone off duty, so it
-- answers on a day off like :floorplan does.
--
-- group_id is a MINIMUM rank: 2 = Builder and every rank above it. Takes
-- effect when the emulator restarts, which the deploy does next.
--
-- Idempotent: an upsert.

INSERT INTO `permissions_commands` (`command`, `group_id`, `subscription_id`)
VALUES ('rp_furni_function', 2, 0)
ON DUPLICATE KEY UPDATE `group_id` = VALUES(`group_id`), `subscription_id` = VALUES(`subscription_id`);

-- Should list one row at 2 / 0.
SELECT `command`, `group_id`, `subscription_id` FROM `permissions_commands`
WHERE `command` = 'rp_furni_function';

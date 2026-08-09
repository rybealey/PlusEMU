-- :alert single-user moderation toast restricted to staff (rank >= 5),
-- matching the project's staff convention. Stock mapping was 2.
UPDATE `permissions_commands` SET `group_id` = '5' WHERE `command` = 'command_alert_user';

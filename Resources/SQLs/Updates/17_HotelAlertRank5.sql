-- :ha hotel alert usable by all staff (rank >= 5), matching the project's
-- staff convention (client isMod / Habbo.IsStaff). Stock mapping was 8.
UPDATE `permissions_commands` SET `group_id` = '5' WHERE `command` = 'command_hotel_alert';

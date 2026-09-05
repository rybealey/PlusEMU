-- pixelrp Movement V2: TURN IT OFF.
--
-- Rollback of 70_EnableMovementV2.sql. Avatars froze on first step during the
-- first beta test. V1 reclaims each user as they move; no restart required.

INSERT INTO `server_settings` (`key`, `value`, `description`) VALUES
('movement.v2.enabled', '0', 'Movement V2: 1 = V2 owns route + timing for human users (bots/pets stay on V1). Anything else = off, V1 owns all movement. Read live; no restart needed.')
ON DUPLICATE KEY UPDATE `value` = '0';

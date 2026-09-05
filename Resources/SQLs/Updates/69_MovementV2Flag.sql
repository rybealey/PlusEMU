-- pixelrp Movement V2: the single kill switch for the new movement system.
--
-- Inserted set to '0' (OFF) so the row EXISTS and can be toggled live from the
-- database without a deploy. It is deliberately not enabled here: turning V2 on
-- is a decision made against a specific beta window, not a side effect of a
-- schema update.
--
-- WHY THE ENCODING IS "enabled iff exactly 1":
-- SettingsManager.TryGetValue returns the STRING "0" for a MISSING key
-- (Core/Settings/SettingsManager.cs:26), so "0" and "absent" are
-- indistinguishable. A flag that had to mean "off" by absence could therefore
-- never be expressed - that is the exact trap the V1 pathfinder.formation.*
-- settings fell into, where 0 had to be reinterpreted as "use the default" and
-- only a NEGATIVE value could disable the feature. V2 avoids it by treating
-- anything that is not exactly "1" as off.
--
-- TO ENABLE ON BETA:
--   UPDATE `server_settings` SET `value` = '1' WHERE `key` = 'movement.v2.enabled';
-- The emulator reads this live, so it takes effect on the next click without a
-- restart. Rolling back is the same statement with '0'.
--
-- SCOPE WHEN ENABLED: V2 takes over route planning and step timing for HUMAN
-- users only. Bots and pets stay on V1. Packet 4110 is not emitted; movement
-- still reaches clients through the existing UserUpdateComposer, so a stock
-- client renders it natively and no client deploy is required.

INSERT IGNORE INTO `server_settings` (`key`, `value`, `description`) VALUES
('movement.v2.enabled', '0', 'Movement V2: 1 = V2 owns route + timing for human users (bots/pets stay on V1). Anything else = off, V1 owns all movement. Read live; no restart needed.');

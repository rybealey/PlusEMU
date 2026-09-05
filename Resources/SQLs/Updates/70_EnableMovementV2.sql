-- pixelrp Movement V2: TURN IT ON for the first beta test.
--
-- Uses INSERT ... ON DUPLICATE KEY UPDATE rather than a bare UPDATE on purpose:
-- if 69_MovementV2Flag.sql did not apply for any reason, a plain UPDATE would
-- match zero rows and silently do nothing, and the test would look like a code
-- failure instead of a missing row. This form works whether the row exists or not.
--
-- Effect: V2 owns route planning and step timing for HUMAN users. Bots and pets
-- stay on V1. No packet 4110 is emitted - movement rides the existing
-- UserUpdateComposer, so a stock client renders it natively.
--
-- The emulator reads this setting live, per call. It takes effect on the next
-- click; no restart. Enrolment happens on ROOM ENTRY, so anyone already standing
-- in a room stays on V1 until they walk out and back in.
--
-- TO TURN IT BACK OFF, either run this against the beta DB:
--   UPDATE `server_settings` SET `value`='0' WHERE `key`='movement.v2.enabled';
-- or add 71_DisableMovementV2.sql with value '0' and push to beta.

INSERT INTO `server_settings` (`key`, `value`, `description`) VALUES
('movement.v2.enabled', '1', 'Movement V2: 1 = V2 owns route + timing for human users (bots/pets stay on V1). Anything else = off, V1 owns all movement. Read live; no restart needed.')
ON DUPLICATE KEY UPDATE `value` = '1';

-- pixelrp: rooms the Room tool knocked down to 25 or 50 visitors go back to 200.
--
-- The Room tool read the wrong visitor number from the server - the most a room
-- may be set to, which the server still worked out the old way (50 for a room
-- bigger than 100 tiles, else 25) - instead of the room's own limit. It sends
-- every setting on every change, so changing anything in the tool, even the
-- name, saved that 25 or 50 as the room's limit. Both ends are fixed; this puts
-- back what 228 gave those rooms.
--
-- 25 is not in the tool's list (10 to 200 in tens), so only this could have set
-- it. 50 is in the list and may have been chosen - it is reset too, by
-- Twist's choice; staff can lower a room again in the Room tool.
--
-- Idempotent.

UPDATE `rooms` SET `users_max` = 200 WHERE `users_max` IN (25, 50);

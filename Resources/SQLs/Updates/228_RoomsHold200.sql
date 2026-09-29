-- pixelrp: every room holds 200 visitors.
--
-- The visitor limit now runs from 10 to 200 (RoomLimits): the Room tool and the
-- room creator list 10 to 200 in tens, and the server clamps to the same range
-- (it used to cap a save at 50 and a new room at 25, so picking 100 silently
-- saved 50). This one-time step sets every existing room to the maximum, and
-- the column's own default becomes 200 so a room inserted any other way starts
-- there too. Staff can still lower a room's limit in the Room tool.

UPDATE `rooms` SET `users_max` = 200;

ALTER TABLE `rooms` ALTER COLUMN `users_max` SET DEFAULT 200;

-- pixelrp: every room allows trading unless its owner turns it off.
--
-- The room creator used to open on "Not allowed" (trade_settings 0), so most
-- rooms were made without trading by nobody's choice. The creator now opens
-- on "Allowed" and CreateFlatEvent falls back to it; this one-time switch
-- brings the rooms that already exist in line. Owners who want trading off
-- set it again in Room settings.
--
-- trade_settings: 0 no trading, 1 only the owner and players with rights,
-- 2 everyone. The column's own DEFAULT is already 2.

UPDATE `rooms` SET `trade_settings` = 2 WHERE `trade_settings` <> 2;

-- PixelRP: a copy of user_statistics.OnlineTime before the online-time fix
-- clears it.
--
-- Habbo.SessionStart was never set (a 2022 upstream refactor dropped the
-- constructor that set it), so every logout and every shutdown save wrote
-- "now - 0 + previous" - a whole unix timestamp, or a value wrapped negative -
-- into OnlineTime. The fix sets it at login, and PlusEnvironment's startup
-- clears every value below 0 or above ten years. That has to happen at
-- STARTUP, not here: this runs while the old server is still up, and its
-- shutdown save writes the wrong values again for everybody online.
--
-- Nothing in the column is believed to be real, but it is overwritten, so it
-- is kept here first. Never written again.
CREATE TABLE IF NOT EXISTS `_online_time_before_fix` AS
    SELECT `id`, `OnlineTime` FROM `user_statistics`;

-- How many rows, and how many of them cannot be real - the startup clear
-- will zero exactly those.
SELECT COUNT(*) AS rows_kept,
       SUM(`OnlineTime` < 0 OR `OnlineTime` > 315360000) AS impossible,
       SUM(`OnlineTime` = 0) AS zero
FROM `_online_time_before_fix`;

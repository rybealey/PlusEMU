-- pixelrp: a turf claim takes five minutes of uncontested time, not one.
--
-- 221 seeded turf.capture.seconds at 60 when a rival walking in failed the
-- claim outright. A rival now CONTESTS it instead - the clock pauses until they
-- leave (TurfManager.Tick) - so the hold is the whole contest, and a minute of
-- it was no contest at all.
--
-- Only moves the value off 221's default: a room owner or staff member who has
-- set their own figure keeps it. Idempotent.
UPDATE `server_settings` SET `value` = '300'
WHERE `key` = 'turf.capture.seconds' AND `value` = '60';

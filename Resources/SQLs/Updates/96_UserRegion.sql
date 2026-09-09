-- pixelrp: the player's region, set from the phone's Settings > General.
--
-- Three codes - 'na', 'eu', 'oc' - or '' for a player who has not picked one,
-- which is what every existing account starts as. Stored on `users` rather
-- than in the phone's own state (user_phone, 91) for one reason: the region
-- shows on OTHER players' profiles, and user_phone is a private per-player
-- document the server never reads a field out of.
--
-- A code rather than a name so the display string lives in the client, where
-- it can be reworded without a migration.
ALTER TABLE `users`
    ADD COLUMN `rp_region` VARCHAR(2) NOT NULL DEFAULT '' AFTER `airplane_mode`;

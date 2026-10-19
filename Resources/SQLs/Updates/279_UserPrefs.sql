-- pixelrp: a player's settings, kept on the account (Twist, 2026-10-19: "all
-- settings and hud colours should be kept by account, just like macros").
--
-- The same shape as user_macros (62): one row per user holding the whole
-- document as JSON - a map from each setting's storage key (the key the client
-- used to keep it under in localStorage) to the string it held. The client
-- owns the document and always saves it whole; nothing server-side reads a
-- setting. RpSavePrefsEvent keeps only the keys it knows, within the limits
-- there, and re-serialises before this is written.
--
-- No row = never saved: the client then sends up whatever its browser had, so
-- nobody loses the settings they made before this.
--
-- MEDIUMTEXT, not TEXT: the re-serialised document escapes quotes and emoji
-- ("), so it can grow to several times the 32 KB the client may send -
-- past TEXT's 64 KB.
CREATE TABLE IF NOT EXISTS `user_prefs` (
  `user_id` INT(11) NOT NULL,
  `data` MEDIUMTEXT NOT NULL,
  PRIMARY KEY (`user_id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

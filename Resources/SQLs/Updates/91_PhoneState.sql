-- pixelrp phone: everything the phone remembers about a player, stored
-- server-side so it follows them to any browser or machine. Until now the
-- home-screen layout, dock, installed apps, wallpaper, theme, open position,
-- accessibility and notification switches, pinned and muted conversations and
-- the Notification Center's history all lived in that browser's localStorage,
-- and a new computer meant a factory-reset phone.
--
-- Two documents per user, both JSON the CLIENT owns and versions (usePhone's
-- PHONE_LAYOUT_VERSION and its per-field readers). The emulator is a locker:
-- it never reads a field, it stores what RpSavePhoneStateEvent has checked is
-- well-formed JSON of the right kind and hands it back at login. Same posture
-- as user_macros (62), for the same reasons - nothing server-side keys on an
-- individual setting, and the client always loads and saves a document whole.
--
-- Two columns rather than one because they change at very different rates:
-- prefs when the player drags an icon or flips a switch, notifications every
-- time something arrives. Keeping them apart means a notification landing
-- does not rewrite the layout, and vice versa.
--
-- '' means "never saved", which the client reads as "seed from my local copy
-- if I have one, else defaults" - the one-time migration off localStorage.
CREATE TABLE IF NOT EXISTS `user_phone` (
  `user_id` INT(11) NOT NULL,
  `prefs` MEDIUMTEXT NOT NULL,
  `notifications` MEDIUMTEXT NOT NULL,
  `updated_at` TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
  PRIMARY KEY (`user_id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

-- pixelrp: who may see the personal details on a player's profile.
--
-- Two fields with separate audiences. The birthday is a real date about a real
-- person and reads no one / contacts / everyone; the region says roughly when
-- somebody is about and reads no one / custom / everyone, where custom is the
-- two lists below it.
--
-- The defaults are deliberately what the hotel did BEFORE this screen existed
-- - region on the profile for anyone, birthday on the calendar for friends -
-- so nobody wakes up sharing less than they were yesterday. A row appears the
-- first time a player saves; until then PrivacyUtility.Default answers.
CREATE TABLE IF NOT EXISTS `rp_user_privacy` (
  `user_id` INT NOT NULL,
  -- 0 = no one, 1 = contacts (the phone's Contacts app, i.e. friends), 2 = everyone
  `birthday_visibility` TINYINT NOT NULL DEFAULT 1,
  -- 0 = no one, 1 = custom (the two flags below), 2 = everyone
  `region_visibility` TINYINT NOT NULL DEFAULT 2,
  `region_colleagues` TINYINT(1) NOT NULL DEFAULT 1,
  `region_friends` TINYINT(1) NOT NULL DEFAULT 1,
  PRIMARY KEY (`user_id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

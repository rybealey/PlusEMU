-- PixelRP: bots are not players (Twist, 2026-10-18).
--
-- The in-hotel AI bot logs in as an ordinary account - Claude on the live
-- hotel, ClaudeTest on beta and in development (compose BOT_USERNAME) - so it
-- turned up in the phone's player search, the friend search and the gang
-- invite list as if it were a player. Marking it as a system account, like
-- the PixelRP account (247), takes it out of every search a player can use:
-- messenger, Sitch, the City panel, the website's search, leaderboards and the
-- website's street walkers. Logging in is untouched - the bot still plays.
-- A gang invite typed out in full is refused too (RpGangInviteEvent).
UPDATE `users` SET `rp_system_account` = 1 WHERE `username` IN ('Claude', 'ClaudeTest');

SELECT `id`, `username`, `rp_system_account` FROM `users` WHERE `username` IN ('Claude', 'ClaudeTest');

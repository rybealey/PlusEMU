-- PixelRP: the PixelRP account - owner of the city's own rooms, where the
-- gameplay is held.
--
-- A room's owner has to be a real account: every room load joins `users` on
-- `rooms`.`owner`, so a room pointing at nobody would not load at all. Staff
-- turn a room into a PixelRP room from the Room Tool (RpMakePixelRpRoomEvent),
-- which hands the room, its furni and its bots to this account.
--
-- Nobody can log in as it. The game refuses an empty login ticket outright
-- (Authenticator: EmptySSO), and the website's password check fails for an
-- empty password before it compares anything. The mail address is in a
-- reserved domain, so a password reset goes nowhere.
--
-- `rp_system_account` marks it as the hotel's own account: every search a
-- player can use leaves it out (messenger, Sitch, the City panel, the
-- website's search and leaderboards). Staff tools still find it.
ALTER TABLE `users`
    ADD COLUMN `rp_system_account` TINYINT(1) NOT NULL DEFAULT 0;

INSERT INTO `users`
    (`username`, `password`, `mail`, `auth_ticket`, `rank`, `credits`, `vip_points`, `activity_points`, `vip`,
     `look`, `gender`, `motto`, `account_created`, `block_newfriends`, `hide_online`, `hide_inroom`,
     `allow_gifts`, `ignore_invites`, `rp_system_account`)
SELECT 'PixelRP', '', 'pixelrp@pixelrp.invalid', '', 1, 0, 0, 0, '0',
       'hd-180-1.ch-210-66.lg-270-82.sh-290-80', 'M', '', UNIX_TIMESTAMP(), '1', '1', '1',
       '0', '1', 1
FROM DUAL
WHERE NOT EXISTS (SELECT 1 FROM `users` WHERE `username` = 'PixelRP');

UPDATE `users` SET `rp_system_account` = 1 WHERE `username` = 'PixelRP';

SELECT `id`, `username`, `rank`, `rp_system_account` FROM `users` WHERE `username` = 'PixelRP';

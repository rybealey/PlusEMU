-- pixelrp: six ranks instead of nine (Twist, 2026-10-20).
--
--   1 User              every player
--   2 Builder           building tools, the Furni Shop and furni inventory
--                       (Habbo.IsBuilder, in code), and building rights in
--                       any room
--   3 Trial Moderator   the moderation commands and the mod tools
--   4 Moderator         was Moderator (3) and Senior Moderator (4)
--   5 Administrator     unchanged - still the "is staff" line in the code
--   6 Developer         was Manager (6), Community Leader (7), Developer (8)
--                       and Owner (9)
--
-- People move with their rank: Piper (Owner) and twist (Community Leader) to
-- Developer, lyn (Manager) to Administrator. Nobody held 2, 3, 4 or 8.
--
-- Commands (permissions_commands.group_id is a MINIMUM rank) move the same
-- way, except that the building tools and the VIP commands (:wd, :pull) among
-- the old Trial Moderator commands come down to Builder, and :unfreeze comes down to Trial Moderator to match
-- :freeze. Manager-level things (housekeeping, editing other
-- staff's news, seeing hidden staff, :makesay, :massbadge) go to Developer,
-- not Administrator.
--
-- Rights (permissions_rights) are NOT a threshold - each rank lists its own,
-- looked up by the EXACT rank - so each new rank gets its old rank's list plus
-- every right of the ranks below it. Builder up to Moderator also get
-- `room_any_rights`, which Room.CheckRights only honours while clocked in at
-- City Government. Developer gets everything Owner had.
--
-- Website side: the CMS reads rank names from `ranks`, and its housekeeping
-- and website permissions are minimum ranks too; 7 (Community Leader) becomes
-- 6. The website_settings rank rows (4, 5, 6, 6) mean the same people as
-- before and stay as they are.
--
-- Takes effect when the emulator restarts, which the deploy does next.
--
-- Idempotent: the moves only run while rank 9 still exists, and rank 9 is
-- removed at the end, all in one transaction.

START TRANSACTION;

SET @old := (SELECT COUNT(*) FROM `permissions_groups` WHERE `id` = 9);

-- People.
UPDATE `users` SET `rank` = CASE `rank`
    WHEN 2 THEN 3
    WHEN 3 THEN 4
    WHEN 4 THEN 4
    WHEN 6 THEN 5
    WHEN 7 THEN 6
    WHEN 8 THEN 6
    WHEN 9 THEN 6
    ELSE `rank` END
WHERE @old > 0 AND `rank` IN (2, 3, 4, 6, 7, 8, 9);

-- Commands.
UPDATE `permissions_commands` SET `group_id` = CASE
    WHEN `group_id` = 2 AND `command` IN (
        'command_coords',      -- your position in the room
        'command_ss',          -- set the state of the furni you face
        'command_setspeed',    -- roller speed
        'command_regen_maps',  -- fix a broken room map
        'command_unload',      -- unload the room
        'command_override',    -- walk through anything
        'command_teleport',    -- teleport within the room
        'command_fastwalk'     -- walk fast
    ) THEN 2
    -- The VIP commands (open to VIPs of any rank: :wd and :pull) go to Builder
    -- too, so Builders have everything a VIP can type.
    WHEN `group_id` = 2 AND `subscription_id` > 0 THEN 2
    WHEN `group_id` = 2 THEN 3
    -- Trial Moderators can :freeze, so they can :unfreeze too.
    WHEN `group_id` = 3 AND `command` = 'command_unfreeze' THEN 3
    WHEN `group_id` IN (3, 4) THEN 4
    WHEN `group_id` IN (7, 8, 9) THEN 6
    ELSE `group_id` END
WHERE @old > 0 AND `group_id` >= 2;

-- Rights: built in a scratch table, then swapped in. Rows pointing at no
-- permission are dropped on the way (the emulator ignores them anyway).
CREATE TEMPORARY TABLE `_six_rank_rights` (
    `group_id` INT NOT NULL,
    `permission_id` INT NOT NULL,
    PRIMARY KEY (`group_id`, `permission_id`)
);

-- Each new rank gets the list of every old rank that lands at or below it:
-- old 2 lands at 3, old 3 and 4 at 4, old 5 at 5, old 6 to 9 at 6.
INSERT IGNORE INTO `_six_rank_rights` (`group_id`, `permission_id`)
SELECT `n`.`id`, `pr`.`permission_id`
FROM `permissions_rights` `pr`
JOIN `permissions` `p` ON `p`.`id` = `pr`.`permission_id`
JOIN (SELECT 3 AS `id` UNION ALL SELECT 4 UNION ALL SELECT 5 UNION ALL SELECT 6) `n`
  ON (CASE `pr`.`group_id`
          WHEN 2 THEN 3
          WHEN 3 THEN 4
          WHEN 4 THEN 4
          WHEN 5 THEN 5
          ELSE 6 END) <= `n`.`id`
WHERE @old > 0 AND `pr`.`group_id` BETWEEN 2 AND 9;

-- Build anywhere, for Builder and up (5 and 6 already have it).
INSERT IGNORE INTO `_six_rank_rights` (`group_id`, `permission_id`)
SELECT `g`.`id`, `p`.`id`
FROM `permissions` `p`
JOIN (SELECT 2 AS `id` UNION ALL SELECT 3 UNION ALL SELECT 4) `g`
WHERE @old > 0 AND `p`.`permission` = 'room_any_rights';

DELETE FROM `permissions_rights` WHERE @old > 0;

INSERT INTO `permissions_rights` (`group_id`, `permission_id`)
SELECT `group_id`, `permission_id` FROM `_six_rank_rights`
ORDER BY `group_id`, `permission_id`;

DROP TEMPORARY TABLE `_six_rank_rights`;

-- The rank list the emulator reads.
INSERT INTO `permissions_groups` (`id`, `name`, `description`, `badge_code`) VALUES
    (1, 'User', 'Every player.', ''),
    (2, 'Builder', 'Building tools, in any room while on duty.', ''),
    (3, 'Trial Moderator', 'Moderation commands and the mod tools.', 'TMOD'),
    (4, 'Moderator', 'Everything a Trial Moderator has, and more.', 'MOD'),
    (5, 'Administrator', 'Staff tools across the hotel.', 'ADM'),
    (6, 'Developer', 'Every permission.', 'DEV')
ON DUPLICATE KEY UPDATE `name` = VALUES(`name`), `description` = VALUES(`description`), `badge_code` = VALUES(`badge_code`);

DELETE FROM `permissions_groups` WHERE `id` > 6;

-- The rank list the website reads (Permission model, plus driver).
INSERT INTO `ranks` (`id`, `name`, `badgeid`, `title`, `tab_colour`) VALUES
    (1, 'User', 'VIP', '', 'green'),
    (2, 'Builders', '', 'Building Staff', 'orange'),
    (3, 'Trial Moderators', 'TMOD', 'Trial Moderation Staff', 'green'),
    (4, 'Moderators', 'MOD', 'Community Moderation Staff', 'settings'),
    (5, 'Administrators', 'ADM', 'Administrative Staff', 'pixeldarkblue'),
    (6, 'Developers', 'DEV', 'Hotel Developers', 'pixellightblue')
ON DUPLICATE KEY UPDATE `name` = VALUES(`name`), `badgeid` = VALUES(`badgeid`), `title` = VALUES(`title`), `tab_colour` = VALUES(`tab_colour`);

DELETE FROM `ranks` WHERE `id` > 6;

-- Website permissions: 7 was the top in use; 6 is the top now.
UPDATE `website_housekeeping_permissions` SET `min_rank` = 6 WHERE `min_rank` > 6;
UPDATE `website_permissions` SET `min_rank` = '6' WHERE CAST(`min_rank` AS UNSIGNED) > 6;

COMMIT;

-- Should list ranks 1-6 with their people, commands and rights.
SELECT `g`.`id`, `g`.`name`,
       (SELECT COUNT(*) FROM `users` `u` WHERE `u`.`rank` = `g`.`id`) AS `people`,
       (SELECT COUNT(*) FROM `permissions_commands` `c` WHERE `c`.`group_id` = `g`.`id`) AS `commands`,
       (SELECT COUNT(*) FROM `permissions_rights` `r` WHERE `r`.`group_id` = `g`.`id`) AS `rights`
FROM `permissions_groups` `g` ORDER BY `g`.`id`;

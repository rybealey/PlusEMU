-- pixelrp: :spit stops being everybody's. Players earn it from a Spit Token
-- (won at events, or put in a backpack by staff with :spawn <user> spit) and
-- redeeming the token unlocks the command for that player for good.
--
-- `group_id` is a RANK THRESHOLD, not a group: 5 is staff, who keep it without
-- a token. 170 set it to 1 and has already run, so this moves it rather than
-- 170 being edited.
--
-- A player's unlocks live in their own table and are added on top of what
-- their rank gives them (PermissionManager.GetCommandsForPlayer). Only command
-- names still in `permissions_commands` count, so deleting a command there
-- retires every unlock of it too.
--
-- Nobody's :spit is taken away mid-session: permissions are built at login, so
-- a player already online keeps it until they next log in.
--
-- Idempotent.
UPDATE `permissions_commands` SET `group_id` = 5 WHERE `command` = 'command_spit';

CREATE TABLE IF NOT EXISTS `user_command_unlocks` (
  `user_id` int(11) NOT NULL,
  `command` varchar(64) NOT NULL,
  `unlocked_at` int(11) NOT NULL DEFAULT 0,
  PRIMARY KEY (`user_id`, `command`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

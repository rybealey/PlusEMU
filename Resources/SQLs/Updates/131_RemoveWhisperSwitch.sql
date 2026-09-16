-- pixelrp: whispers are always on, so the switch that turned them off is gone.

-- Habbo.ReceiveWhispers was a plain bool that nothing ever filled in. The login
-- query in UserDataFactory hydrates every sibling setting from the users table -
-- allow_gifts, allow_mimic, pets_muted, bots_muted - but there was never a
-- column for this one, so it started false on every login and WhisperEvent
-- refused every whisper with "this user has their whispers disabled". Since the
-- first commit, for everybody.

-- The only thing that ever wrote to it was :disablewhispers, which flipped the
-- value rather than setting it - so the command named "disable" was the one that
-- enabled whispers, and it saved nothing, so the effect died at logout. It also
-- had no permissions_commands row in the shipped schema, and a command with no
-- row cannot be run by anyone: on a fresh database there was no way to turn
-- whispers on at all. The DELETE below is for databases where somebody added the
-- row by hand.

-- room_whisper_override existed only to let staff past that check. With the
-- check deleted it grants nothing, so it goes rather than sit in the table
-- looking like it still means something - same reasoning as 125.

-- The command class and the property are deleted in the same commit. Commands
-- are discovered by assembly scan rather than a registration list, so the file
-- going away is the whole removal on the emulator side; these rows are what
-- would otherwise be left behind.

-- Not removed: the ignore list still stops a whisper reaching somebody who
-- ignored the sender, and it is now the only way to do that. Mutes, flood
-- control, the word filter and the banned-phrase ban all run before this point
-- and are untouched. IgnorePublicWhispers and :ignorewhispers are a different
-- setting despite the name - staff choosing whether to see other people's
-- whispers - and stay exactly as they are.

-- Idempotent: deleting a row that is already gone is not an error. Matched by
-- name rather than by id 39, in case this database numbered it differently.

DELETE FROM `permissions_commands` WHERE `command` = 'command_disable_whispers';

DELETE FROM `permissions_rights`
WHERE `permission_id` IN (SELECT `id` FROM (SELECT `id` FROM `permissions` WHERE `permission` = 'room_whisper_override') AS p);

DELETE FROM `permissions` WHERE `permission` = 'room_whisper_override';

-- All three should return nothing.
SELECT `command`, `group_id` FROM `permissions_commands` WHERE `command` = 'command_disable_whispers';
SELECT `id`, `permission` FROM `permissions` WHERE `permission` = 'room_whisper_override';
SELECT r.`group_id`, r.`permission_id` FROM `permissions_rights` r
  LEFT JOIN `permissions` p ON p.`id` = r.`permission_id` WHERE p.`id` IS NULL;

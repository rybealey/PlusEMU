-- Room walls are hidden by default. New rooms (the INSERT in
-- RoomManager.CreateRoom does not name the column) now pick up the hidden
-- default, and existing rooms with allow_hidewall = 0 are flipped: until the
-- RoomFactory tinyint(1) parsing fix landed alongside this update, walls
-- always rendered regardless of the stored value, so a 0 here is the old
-- default rather than a deliberate "show walls" choice. Owners who want
-- walls back can re-enable them in room settings.
--
-- Idempotent: both statements converge on the same end state.

ALTER TABLE `rooms` ALTER COLUMN `allow_hidewall` SET DEFAULT 1;

UPDATE `rooms` SET `allow_hidewall` = 1 WHERE `allow_hidewall` = 0;

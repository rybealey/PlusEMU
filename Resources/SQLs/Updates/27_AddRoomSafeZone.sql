-- PixelRP zone type: rooms flagged as safe zones freeze the passive-status
-- countdown for everyone inside (RoomUserManager re-anchors the decrement
-- clock while the room is safe). Set from Room settings > Roleplay >
-- Zone Type (owner only, RpRoomZoneSaveEvent). '0' = unsafe (default).
ALTER TABLE `rooms` ADD COLUMN `is_safe_zone` enum('0','1') NOT NULL DEFAULT '0';

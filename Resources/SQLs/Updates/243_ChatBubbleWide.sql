-- pixelrp: one chat bubble width hotel-wide - Wide, the column's own default.
-- (`rooms`.`chat_size` is the client's RoomChatSettings width: 0 Wide, 1
-- Normal, 2 Thin; ChatWidgetMessageView caps a bubble at 2000, 350 and 240px.)
--
-- The Room tool no longer offers the setting (its Chat section is gone), so a
-- room once set to Normal or Thin would have kept that for good. The default is
-- already 0, so rooms made from now on are Wide as before; this only brings
-- every existing room in line.
--
-- Idempotent: an absolute assignment.

UPDATE `rooms` SET `chat_size` = 0 WHERE `chat_size` <> 0;

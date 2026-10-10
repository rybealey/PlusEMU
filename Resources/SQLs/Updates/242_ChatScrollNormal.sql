-- pixelrp: chat scrolls at Normal speed hotel-wide - bubbles step up every 6
-- seconds instead of Fast's 3. (`rooms`.`chat_speed` is the client's
-- RoomChatSettings speed: 0 Fast, 1 Normal, 2 Slow; useChatWidget's
-- getScrollSpeed makes those 3000, 6000 and 12000 ms between 15px steps.)
--
-- Every room now - Slow included, as asked: Normal hotel-wide - and the column
-- default for every room made after it, since room creation (RoomManager)
-- leaves chat_speed to the default. A single room can still be changed in the
-- Room tool's Scroll speed.
--
-- Idempotent: absolute assignments. SET DEFAULT is a metadata change, so the
-- rooms table is not rebuilt.

ALTER TABLE `rooms` ALTER COLUMN `chat_speed` SET DEFAULT 1;
UPDATE `rooms` SET `chat_speed` = 1 WHERE `chat_speed` <> 1;

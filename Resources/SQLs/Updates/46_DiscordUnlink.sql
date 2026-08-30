-- In-game Discord disconnect (Settings > Social > Verification > Discord).
-- An unlink row must remember WHICH Discord account to strip, because the
-- emulator clears `users.discord_id` the moment the player disconnects and
-- `discord:sweep` only ever iterates users that are still linked - this
-- queue row is the only cleanup path there is.

ALTER TABLE `discord_sync_queue`
  ADD COLUMN `discord_id` varchar(32) NULL DEFAULT NULL;

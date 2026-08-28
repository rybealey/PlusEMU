-- PixelRP airplane mode: a per-account toggle from the phone's Settings app.
-- While on, incoming friend requests are hidden in the phone's Contacts app,
-- and direct messages sent to the player fail to deliver (the sender sees a
-- red "Not Delivered" receipt). Persisted per user; off by default.
ALTER TABLE `users`
  ADD COLUMN `airplane_mode` TINYINT(1) NOT NULL DEFAULT 0;

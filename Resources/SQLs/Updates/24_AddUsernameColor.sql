-- PixelRP username color (Settings > Social > Username > Color). Each user's
-- chosen name color for their own chat bubbles; empty = default (black). It is
-- loaded at login (RpUiSettings) and stamped onto the chat packet so every
-- player in the room sees it on the sender's username in bubbles and history.
ALTER TABLE `user_ui_settings`
  ADD COLUMN `username_color` VARCHAR(7) NOT NULL DEFAULT '' AFTER `header_color`;

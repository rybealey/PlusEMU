-- PixelRP username icon: an optional FontAwesome-kit icon rendered as
-- [ <icon> ] before the player's name in chat, plus its color. Empty icon =
-- none (no prefix); empty color = default (black). Loaded at login and carried
-- on the chat packet so everyone in the room sees it.
ALTER TABLE `user_ui_settings`
  ADD COLUMN `username_icon` VARCHAR(64) NOT NULL DEFAULT '' AFTER `username_color`,
  ADD COLUMN `username_icon_color` VARCHAR(7) NOT NULL DEFAULT '' AFTER `username_icon`;

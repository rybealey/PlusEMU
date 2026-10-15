-- pixelrp: Settings > UI > Colours lets a player pick any window header
-- colour, saved as '#rrggbb' beside the old preset keys (orange, pink,
-- purple). Make sure the column holds seven characters.
UPDATE `user_ui_settings` SET `header_color` = '' WHERE `header_color` IS NULL;
ALTER TABLE `user_ui_settings`
  MODIFY COLUMN `header_color` VARCHAR(16) NOT NULL DEFAULT '';

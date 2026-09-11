-- pixelrp: one account, up to three player characters.
--
-- A character IS a users row - everything in the hotel keys off users.id and
-- keeps working untouched. Two columns turn a set of rows into an account:
--
--   parent_id           NULL = this row is an account root. Otherwise the root
--                       it belongs to. Depth is exactly one, so resolving an
--                       account is `parent_id ?? id` and there is no tree.
--   active_character_id Root rows only: which character the next /game load
--                       enters as. The CMS reads it to pick whose SSO ticket
--                       to issue, so it is also "the last one played".
--
-- Every existing row stays its own root (parent_id NULL). Nothing changes
-- behaviour on this migration alone.
ALTER TABLE `users`
    ADD COLUMN `parent_id` INT NULL DEFAULT NULL AFTER `id`,
    ADD COLUMN `active_character_id` INT NULL DEFAULT NULL AFTER `parent_id`,
    ADD KEY `idx_users_parent` (`parent_id`);

-- The look a new character starts in, per gender. `start_look` already exists
-- and is what registration uses; these two are what the Wallet's creator uses,
-- because the picker offers a gendered starting outfit and figuredata's sets
-- are gendered. Editable in housekeeping like any other setting.
INSERT INTO `website_settings` (`key`, `value`)
VALUES
  ('start_look_male', 'hr-100-61.hd-180-1.ch-210-66.lg-270-110.sh-305-62'),
  ('start_look_female', 'hr-515-33.hd-600-1.ch-635-70.lg-716-66.sh-735-68')
ON DUPLICATE KEY UPDATE `key` = `key`;

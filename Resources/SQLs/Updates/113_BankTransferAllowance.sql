-- PixelRP: taking money back OUT of savings is rationed.
--
-- Paying in is free and unlimited. Three moves a week come back out.
--
-- The limit runs one way on purpose. Savings is the account that compounds
-- and the only one with a ceiling, so it is meant to be a commitment rather
-- than a second wallet. Rationing only the way out is what makes the two
-- accounts genuinely different - without it savings is just checking that
-- pays interest, and the ATM already exists for money you want to reach.
--
-- Three is enough to cover a change of mind and not enough to run a business
-- out of.
--
-- `transfers_reset_at` is the unix moment the current window ENDS, not when it
-- started, so a stale row is spotted with one comparison and needs no calendar
-- maths in SQL. It is rolled forward lazily - whenever the row is read or
-- written and the moment has passed - which means an account nobody touched
-- for a month still opens on a full allowance rather than owing a catch-up.
--
-- 0 in both columns is what every existing row gets, and the first read rolls
-- it to a real window. That is the same as a fresh account, which is the
-- correct starting state for accounts that predate the rule.

ALTER TABLE `rp_bank_accounts`
    ADD COLUMN `transfers_used` INT NOT NULL DEFAULT 0 AFTER `savings_balance`,
    ADD COLUMN `transfers_reset_at` INT NOT NULL DEFAULT 0 AFTER `transfers_used`;

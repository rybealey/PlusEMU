-- PixelRP: player banking - a current account and a savings account, opened
-- together, owned by a CHARACTER.
--
-- Keyed on `user_id`, which since multi-character is a character row and not
-- an account root. Two characters on one login have two separate banks with
-- no path between them: money is something a character earned, and letting it
-- slide between siblings would make every wage decision meaningless.
--
-- Why this is NOT a column on `users`: `users`.`credits` is written
-- ABSOLUTELY at logout from the in-memory Habbo.Credits, as part of one big
-- overwrite statement. Anything stored there out of band is silently reverted
-- by the next disconnect. Bank money therefore lives only here, is only ever
-- mutated RELATIVELY, and is never reconciled against `credits`.
--
-- One row per character rather than one row per account. The two accounts are
-- opened together and can never exist apart, so a transfer between them is a
-- single atomic UPDATE whose WHERE clause IS the sufficient-funds test, and
-- "has an account" is simply the primary key existing. A half-open account is
-- not representable.
--
-- Balances are bigint because savings compounds and an int would wrap. The
-- emulator clamps both to the savings ceiling before anything reaches the
-- wire, where a balance is a 32-bit integer.

CREATE TABLE IF NOT EXISTS `rp_bank_accounts` (
  `user_id` int NOT NULL,
  `opened_at` int NOT NULL DEFAULT 0,
  `current_balance` bigint NOT NULL DEFAULT 0,
  `savings_balance` bigint NOT NULL DEFAULT 0,
  -- ONLINE seconds banked toward the next interest hour. Persisted rather
  -- than held in memory so "per hour of online time" means the same thing to
  -- somebody who plays in twenty minute sittings as to somebody who does not.
  `savings_seconds` int NOT NULL DEFAULT 0,
  -- unix seconds; the clock the accrual diffs against. The diff is clamped
  -- per tick, so a restart or a week offline can never be claimed as play.
  `last_interest_at` int NOT NULL DEFAULT 0,
  `interest_total` bigint NOT NULL DEFAULT 0,
  `wages_total` bigint NOT NULL DEFAULT 0,
  PRIMARY KEY (`user_id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

-- Every movement of bank money, including the ones nobody asked for. The
-- account row carries a balance and no history, so this is the ONLY evidence
-- a player disputing a figure has.
--
-- One row per ACCOUNT touched, not per submit: a transfer is two rows sharing
-- a timestamp, which makes "where did the money in savings come from" one
-- indexed WHERE rather than a reconstruction. Same reasoning as
-- 103_FurniFunction.sql.
--
-- `username` and `source` are denormalised so the log still reads after a
-- rename, after a corporation is renamed, and after a character is deleted.
CREATE TABLE IF NOT EXISTS `rp_bank_transactions` (
  `id` bigint NOT NULL AUTO_INCREMENT,
  `user_id` int NOT NULL,
  `username` varchar(32) NOT NULL DEFAULT '',
  -- open | wages | interest | transfer_in | transfer_out | deposit | withdraw
  `kind` varchar(24) NOT NULL,
  -- current | savings
  `account` varchar(8) NOT NULL,
  -- signed: positive into the account, negative out of it
  `amount` bigint NOT NULL,
  `balance_after` bigint NOT NULL,
  -- free context: the corporation for wages, the rate for interest, the room
  -- for an ATM. Written to be read by a person, never parsed.
  `source` varchar(96) NOT NULL DEFAULT '',
  `created_at` int NOT NULL DEFAULT 0,
  PRIMARY KEY (`id`),
  KEY `user_id_id` (`user_id`, `id`),
  KEY `created_at` (`created_at`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

-- The ATM furni imported by 102_ATMMachine.sql was scenery: interaction_type
-- 'default', nothing in the emulator special-casing the classname. It becomes
-- a BEHAVIOUR any furni can carry, the same route the jukebox took in
-- 108_JukeboxBehaviour.sql, so a bank set can put a teller window or a wall
-- panel on the same function without a code change.
UPDATE `furniture` SET `interaction_type` = 'atm'
WHERE `item_name` = 'atm_moneymachine' LIMIT 1;

-- Shifts: seconds banked toward the CURRENT 10-minute pay interval. Resets
-- on payout (overflow carries). shift_seconds / shift_seconds_week keep the
-- lifetime and weekly totals; on_duty is cleared at boot (stale on crash).

ALTER TABLE `rp_corporation_employees`
  ADD COLUMN `pay_seconds` int NOT NULL DEFAULT 0 AFTER `shift_seconds_week`;

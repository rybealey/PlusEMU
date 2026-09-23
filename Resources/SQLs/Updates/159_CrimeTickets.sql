-- pixelrp: some crimes can be settled with a ticket instead of a cell.
--
-- `ticketable` is whether the player may pay instead of serving the crime's
-- `jail_seconds`; `ticket_amount` is what they pay, in whole dollars - the
-- hotel's currency, the same units as a player's cash. Housekeeping only
-- asks for an amount once a crime is ticketable, and stores 0 when it is not,
-- so "ticketable with no price" never exists.
--
-- A ticket does not need a sentence behind it: a crime with no jail time can
-- still carry a fine (speeding is the obvious one).
--
-- Like `jail_seconds` before it, this is carried rather than applied: nothing
-- in the hotel arrests or fines anyone yet. It is the price waiting for that
-- to land. Every existing crime starts out not ticketable.
ALTER TABLE `rp_crimes`
    ADD COLUMN `ticketable` TINYINT(1) NOT NULL DEFAULT 0 AFTER `stackable`,
    ADD COLUMN `ticket_amount` INT NOT NULL DEFAULT 0 AFTER `ticketable`;

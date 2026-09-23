-- pixelrp: Pixel Cash - sending money to another player from Messages.
--
-- WHY THIS IS A TABLE AND NOT A MESSENGER MESSAGE. Photos and jam invites
-- ride the messenger as a marker in the message TEXT, which the receiving
-- client turns back into a card. That is safe for a photo, because the client
-- only renders URLs under the hotel's own camera base - there is an origin to
-- check. Money has no such origin: anyone could type "[pay]50000" and scam on
-- the strength of the card it drew. And this emulator's console packet carries
-- sender, text and age, with no extra-data field to hide a signature in.
--
-- So a payment is its own record. The card is drawn from THESE rows, which
-- only the server writes, and a message that merely looks like one draws
-- nothing. The rows are also the moderation trail: structured, and unlike a
-- chat log, not something either party can edit or delete.
--
-- The money itself still moves through rp_bank_accounts and is logged in
-- rp_bank_transactions like every other movement - this table records that the
-- movement was a person-to-person payment and what was said with it.

CREATE TABLE IF NOT EXISTS `rp_pay_transfers` (
  `id` int(11) NOT NULL AUTO_INCREMENT,
  `sender_id` int(11) NOT NULL,
  `recipient_id` int(11) NOT NULL,
  -- Whole credits. Checking only, never savings - savings is rationed by
  -- design and a payment out of it would be a way around that ration.
  `amount` int(11) NOT NULL,
  -- The sender's note. Optional, and short on purpose: it is a memo line on a
  -- receipt, not a message - there is a whole conversation around it for that.
  `note` varchar(64) NOT NULL DEFAULT '',
  `created_at` int(11) NOT NULL,
  PRIMARY KEY (`id`),
  -- The one read the thread does: everything between these two people, in
  -- order. Both directions, so it is indexed from both ends.
  KEY `between_out` (`sender_id`, `recipient_id`, `id`),
  KEY `between_in` (`recipient_id`, `sender_id`, `id`),
  -- The daily cap, which is a SUM over one sender since midnight rather than
  -- a counter anywhere: a counter has to be reset by something, and whatever
  -- resets it is the thing that breaks.
  KEY `sender_day` (`sender_id`, `created_at`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

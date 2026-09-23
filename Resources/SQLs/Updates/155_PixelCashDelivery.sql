-- pixelrp Pixel Cash: a payment reaches a recipient who was offline.
--
-- A payment is a message as far as the phone is concerned - it lights the
-- Messages badge and moves the conversation up. For someone online the live
-- receipt does that. For someone offline nothing did: the receipt had nobody
-- to go to, and a payment is not in messenger_offline_messages, so they logged
-- back in to no sign of it until they happened to open that conversation.
--
-- `delivered` is whether the recipient's client has been handed it. Set on
-- insert when they are online, otherwise at their next messenger init, which
-- is where offline messages are handed over too.
ALTER TABLE `rp_pay_transfers`
  ADD COLUMN `delivered` tinyint(1) NOT NULL DEFAULT 0,
  ADD KEY `undelivered` (`recipient_id`, `delivered`);

-- Everything already sent has been seen, or is in a conversation the
-- recipient has had every chance to open. Nobody logs in to a backlog.
UPDATE `rp_pay_transfers` SET `delivered` = 1;

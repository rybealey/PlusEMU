-- pixelrp: indexes for the housekeeping panel's log explorer, Player 360
-- timeline and justice desk (cms feat/housekeeping-overhaul). They read every
-- log table newest first, by player and by time, and page with a cursor on
-- (time, id); without these a "last hour" filter reads the whole table.
-- 292 already added chatlogs (room_id, timestamp), rp_charges officer_id and
-- (dropped_at, user_id), rp_browser_guard_log created_at and bans
-- (bantype, expire); this adds the rest. Indexes only - no row is changed.
--
--   chatlogs               user_id, timestamp     a player's chat on the timeline
--                          timestamp              the explorer's time window
--   chatlogs_console       from_id, to_id, timestamp  a private conversation, both ways
--   logs_client_staff      user_id, timestamp     a staff member's tool use; the staff audit
--                          timestamp
--   logs_client_trade      1id / 2id, timestamp   a player's trades; the time window
--   logs_client_namechange user_id, timestamp     a player's renames; the time window
--   rp_catalog_move_log    user_id, moved_at      who moved catalog items
--   rp_911_calls           created_at             the 911 feed and Today's unanswered calls
--                          responder_id, created_at  calls an officer answered
--   rp_charges             charged_at             the time window
--   rp_jail                released_at            who is in a cell now (Today, the justice desk)
--                          officer_id, jailed_at  arrests an officer made
--                          jailed_at              the time window
--   rp_trades, rp_pay_transfers, rp_bin_search_log, rp_packet_flood_log
--                          created_at             the time window
--   rp_coin_ledger, rp_bank_transactions
--                          user_id, created_at    a player's money over a week
--   moderation_tickets     timestamp              the tickets list
--
-- Like 292: each ALTER adds every index its table gets in one pass, and only
-- when the first of them isn't there yet, so a re-run is skipped.

SET @sql := IF((SELECT COUNT(*) FROM information_schema.STATISTICS
    WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'chatlogs' AND INDEX_NAME = 'user_time') = 0,
  'ALTER TABLE `chatlogs`
     ADD KEY `user_time` (`user_id`, `timestamp`),
     ADD KEY `timestamp` (`timestamp`)',
  'DO 0');
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;

SET @sql := IF((SELECT COUNT(*) FROM information_schema.STATISTICS
    WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'chatlogs_console' AND INDEX_NAME = 'pair_time') = 0,
  'ALTER TABLE `chatlogs_console` ADD KEY `pair_time` (`from_id`, `to_id`, `timestamp`)',
  'DO 0');
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;

SET @sql := IF((SELECT COUNT(*) FROM information_schema.STATISTICS
    WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'logs_client_staff' AND INDEX_NAME = 'user_time') = 0,
  'ALTER TABLE `logs_client_staff`
     ADD KEY `user_time` (`user_id`, `timestamp`),
     ADD KEY `timestamp` (`timestamp`)',
  'DO 0');
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;

SET @sql := IF((SELECT COUNT(*) FROM information_schema.STATISTICS
    WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'logs_client_trade' AND INDEX_NAME = 'user_one') = 0,
  'ALTER TABLE `logs_client_trade`
     ADD KEY `user_one` (`1id`),
     ADD KEY `user_two` (`2id`),
     ADD KEY `timestamp` (`timestamp`)',
  'DO 0');
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;

SET @sql := IF((SELECT COUNT(*) FROM information_schema.STATISTICS
    WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'logs_client_namechange' AND INDEX_NAME = 'user_time') = 0,
  'ALTER TABLE `logs_client_namechange`
     ADD KEY `user_time` (`user_id`, `timestamp`),
     ADD KEY `timestamp` (`timestamp`)',
  'DO 0');
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;

SET @sql := IF((SELECT COUNT(*) FROM information_schema.STATISTICS
    WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'rp_catalog_move_log' AND INDEX_NAME = 'user_time') = 0,
  'ALTER TABLE `rp_catalog_move_log` ADD KEY `user_time` (`user_id`, `moved_at`)',
  'DO 0');
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;

SET @sql := IF((SELECT COUNT(*) FROM information_schema.STATISTICS
    WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'rp_911_calls' AND INDEX_NAME = 'created') = 0,
  'ALTER TABLE `rp_911_calls`
     ADD KEY `created` (`created_at`),
     ADD KEY `responder` (`responder_id`, `created_at`)',
  'DO 0');
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;

SET @sql := IF((SELECT COUNT(*) FROM information_schema.STATISTICS
    WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'rp_charges' AND INDEX_NAME = 'charged') = 0,
  'ALTER TABLE `rp_charges` ADD KEY `charged` (`charged_at`)',
  'DO 0');
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;

SET @sql := IF((SELECT COUNT(*) FROM information_schema.STATISTICS
    WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'rp_jail' AND INDEX_NAME = 'released') = 0,
  'ALTER TABLE `rp_jail`
     ADD KEY `released` (`released_at`),
     ADD KEY `officer_time` (`officer_id`, `jailed_at`),
     ADD KEY `jailed` (`jailed_at`)',
  'DO 0');
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;

SET @sql := IF((SELECT COUNT(*) FROM information_schema.STATISTICS
    WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'rp_trades' AND INDEX_NAME = 'created') = 0,
  'ALTER TABLE `rp_trades` ADD KEY `created` (`created_at`)',
  'DO 0');
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;

SET @sql := IF((SELECT COUNT(*) FROM information_schema.STATISTICS
    WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'rp_pay_transfers' AND INDEX_NAME = 'created') = 0,
  'ALTER TABLE `rp_pay_transfers` ADD KEY `created` (`created_at`)',
  'DO 0');
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;

SET @sql := IF((SELECT COUNT(*) FROM information_schema.STATISTICS
    WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'rp_bin_search_log' AND INDEX_NAME = 'created') = 0,
  'ALTER TABLE `rp_bin_search_log` ADD KEY `created` (`created_at`)',
  'DO 0');
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;

SET @sql := IF((SELECT COUNT(*) FROM information_schema.STATISTICS
    WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'rp_packet_flood_log' AND INDEX_NAME = 'created') = 0,
  'ALTER TABLE `rp_packet_flood_log` ADD KEY `created` (`created_at`)',
  'DO 0');
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;

SET @sql := IF((SELECT COUNT(*) FROM information_schema.STATISTICS
    WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'rp_coin_ledger' AND INDEX_NAME = 'user_time') = 0,
  'ALTER TABLE `rp_coin_ledger` ADD KEY `user_time` (`user_id`, `created_at`)',
  'DO 0');
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;

SET @sql := IF((SELECT COUNT(*) FROM information_schema.STATISTICS
    WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'rp_bank_transactions' AND INDEX_NAME = 'user_time') = 0,
  'ALTER TABLE `rp_bank_transactions` ADD KEY `user_time` (`user_id`, `created_at`)',
  'DO 0');
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;

SET @sql := IF((SELECT COUNT(*) FROM information_schema.STATISTICS
    WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'moderation_tickets' AND INDEX_NAME = 'timestamp') = 0,
  'ALTER TABLE `moderation_tickets` ADD KEY `timestamp` (`timestamp`)',
  'DO 0');
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;

-- pixelrp: indexes for queries that read whole tables today. Indexes only -
-- no row is changed or removed. Each ALTER adds every index its table gets in
-- one pass, and only when the first of them isn't there yet, so a re-run (or
-- a table already done when an earlier run stopped) is skipped.
--
--   user_roomvisits  room_id                      deleting a room (DeleteRoomEvent), which
--                                                  locked the whole table and stalled the
--                                                  tick writer behind it
--                    user_id, room_id, exit       the exit stamp on every room change (RoomTickWriter)
--                    user_id, entry_timestamp     the mod tool's visit list
--   chatlogs         room_id, timestamp           the mod tool's chatlog per visit
--   rp_charges       dropped_at, user_id          the Wanted list (open charges) on every log in/out
--                    officer_id                   the police leaderboard
--   messenger_offline_messages  to_id             read and deleted on every messenger start
--   users            vip_points, rp_system_account  Diamonds ranking, leaderboard exclusions
--   user_rp_stats    strength_level, stamina_level  the gym leaderboards
--   rp_corporation_employees  shift_seconds       the work leaderboard
--   rp_browser_guard_log  created_at              nginx's asset pause poll, every 10s
--   bans             bantype, expire              active bans (the same poll, check.ban)
--   rp_sitch_activity  kind, created_at           Sitch trending
--   rp_sitch_tags    created_at                   Sitch trending tags
--   rooms            corporation_id               the hospital's HQ lookup on auto-admission

SET @sql := IF((SELECT COUNT(*) FROM information_schema.STATISTICS
    WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'user_roomvisits' AND INDEX_NAME = 'room_id') = 0,
  'ALTER TABLE `user_roomvisits`
     ADD KEY `room_id` (`room_id`),
     ADD KEY `user_room_exit` (`user_id`, `room_id`, `exit_timestamp`),
     ADD KEY `user_entry` (`user_id`, `entry_timestamp`)',
  'DO 0');
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;

SET @sql := IF((SELECT COUNT(*) FROM information_schema.STATISTICS
    WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'chatlogs' AND INDEX_NAME = 'room_time') = 0,
  'ALTER TABLE `chatlogs` ADD KEY `room_time` (`room_id`, `timestamp`)',
  'DO 0');
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;

SET @sql := IF((SELECT COUNT(*) FROM information_schema.STATISTICS
    WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'rp_charges' AND INDEX_NAME = 'idx_charges_open') = 0,
  'ALTER TABLE `rp_charges`
     ADD KEY `idx_charges_open` (`dropped_at`, `user_id`),
     ADD KEY `idx_charges_officer` (`officer_id`)',
  'DO 0');
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;

SET @sql := IF((SELECT COUNT(*) FROM information_schema.STATISTICS
    WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'messenger_offline_messages' AND INDEX_NAME = 'to_id') = 0,
  'ALTER TABLE `messenger_offline_messages` ADD KEY `to_id` (`to_id`)',
  'DO 0');
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;

SET @sql := IF((SELECT COUNT(*) FROM information_schema.STATISTICS
    WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'users' AND INDEX_NAME = 'idx_users_vip_points') = 0,
  'ALTER TABLE `users`
     ADD KEY `idx_users_vip_points` (`vip_points`),
     ADD KEY `idx_users_system_account` (`rp_system_account`)',
  'DO 0');
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;

SET @sql := IF((SELECT COUNT(*) FROM information_schema.STATISTICS
    WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'user_rp_stats' AND INDEX_NAME = 'idx_stats_strength') = 0,
  'ALTER TABLE `user_rp_stats`
     ADD KEY `idx_stats_strength` (`strength_level`),
     ADD KEY `idx_stats_stamina` (`stamina_level`)',
  'DO 0');
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;

SET @sql := IF((SELECT COUNT(*) FROM information_schema.STATISTICS
    WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'rp_corporation_employees' AND INDEX_NAME = 'idx_corp_shift') = 0,
  'ALTER TABLE `rp_corporation_employees` ADD KEY `idx_corp_shift` (`shift_seconds`)',
  'DO 0');
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;

SET @sql := IF((SELECT COUNT(*) FROM information_schema.STATISTICS
    WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'rp_browser_guard_log' AND INDEX_NAME = 'created_at') = 0,
  'ALTER TABLE `rp_browser_guard_log` ADD KEY `created_at` (`created_at`)',
  'DO 0');
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;

SET @sql := IF((SELECT COUNT(*) FROM information_schema.STATISTICS
    WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'bans' AND INDEX_NAME = 'bantype_expire') = 0,
  'ALTER TABLE `bans` ADD KEY `bantype_expire` (`bantype`, `expire`)',
  'DO 0');
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;

SET @sql := IF((SELECT COUNT(*) FROM information_schema.STATISTICS
    WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'rp_sitch_activity' AND INDEX_NAME = 'kind_created') = 0,
  'ALTER TABLE `rp_sitch_activity` ADD KEY `kind_created` (`kind`, `created_at`)',
  'DO 0');
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;

SET @sql := IF((SELECT COUNT(*) FROM information_schema.STATISTICS
    WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'rp_sitch_tags' AND INDEX_NAME = 'created_at') = 0,
  'ALTER TABLE `rp_sitch_tags` ADD KEY `created_at` (`created_at`)',
  'DO 0');
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;

SET @sql := IF((SELECT COUNT(*) FROM information_schema.STATISTICS
    WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'rooms' AND INDEX_NAME = 'idx_rooms_corporation') = 0,
  'ALTER TABLE `rooms` ADD KEY `idx_rooms_corporation` (`corporation_id`)',
  'DO 0');
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;

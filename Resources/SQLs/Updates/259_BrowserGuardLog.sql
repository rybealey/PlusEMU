-- pixelrp: every time the client's browser guard sends a player back to the
-- website - DevTools opened (a shortcut, or found open) or an automated
-- browser - the client says so on the way out, and the emulator writes it
-- here (BrowserGuardLog, RpBrowserGuardEvent). A lead for staff, not proof:
-- the client sends it, so it can be blocked. At most 5 rows per player per
-- 10 minutes.
--
-- reason: 1 DevTools shortcut, 2 DevTools open, 3 automated browser.
-- detail: which shortcut or watch, and the browser's user agent.
--
-- Idempotent: CREATE TABLE IF NOT EXISTS.

CREATE TABLE IF NOT EXISTS `rp_browser_guard_log` (
  `id` int NOT NULL AUTO_INCREMENT,
  `user_id` int NOT NULL,
  `username` varchar(64) NOT NULL DEFAULT '',
  `reason` tinyint NOT NULL,
  `detail` varchar(255) NOT NULL DEFAULT '',
  `created_at` int NOT NULL,
  PRIMARY KEY (`id`),
  KEY `user_created` (`user_id`, `created_at`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

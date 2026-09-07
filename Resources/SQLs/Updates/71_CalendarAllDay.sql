-- pixelrp: calendar events can run all day (shown in the day's all-day row
-- with birthdays instead of on the timeline).
ALTER TABLE `rp_events` ADD COLUMN `all_day` tinyint(1) NOT NULL DEFAULT 0 AFTER `ends_at`;

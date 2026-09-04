-- pixelrp: gangs-on-groups slice 2 (see the parent repo's
-- docs/superpowers/specs/2026-09-04-gangs-on-groups-design.md).
-- A gang IS a group flagged is_gang; gang colour1/colour2 hold RAW RGB
-- ints (stock groups store groups_items colour ids there instead).

ALTER TABLE `groups`
    ADD COLUMN `is_gang` enum('0','1') NOT NULL DEFAULT '0' AFTER `forum_enabled`;

-- Gang creation price in credits; the emulator falls back to 500 when the
-- row is missing or zero.
INSERT INTO `server_settings` (`key`, `value`, `description`)
VALUES ('gang.cost', '500', 'pixelrp: credits charged to found a gang from the Gang window')
ON DUPLICATE KEY UPDATE `value` = `value`;

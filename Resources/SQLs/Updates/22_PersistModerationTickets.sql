-- Moderation tickets are now persisted, so they survive an emulator restart.
-- Two fields on a ticket have nowhere to live in the shipped table:
--   category       the CFH topic the reporter picked. The existing `type`
--                  column holds the other category field, which is the one
--                  the client displays in the Open Issues list.
--   reported_chats the chat lines the reporter quoted as evidence, stored as
--                  a JSON array. Nullable: a NULL or unreadable value loads
--                  as no chats rather than failing startup.

-- Each column is added only if it is missing: production's table is migrated
-- from Arcturus and not guaranteed to lack either one (some forks already carry
-- `category`). A plain ALTER on an existing column is an error, and the deploy
-- workflow runs these files under `set -e` — one duplicate column would abort
-- the whole "Applying database patches" step and fail the deploy. Guarding each
-- statement lets a partially present schema pick up whichever column it is
-- missing, and makes the file safe to re-run.

SET @add_category := (
    SELECT IF(COUNT(*) > 0,
        'DO 0',
        'ALTER TABLE `moderation_tickets` ADD COLUMN `category` int(11) NOT NULL DEFAULT 0 AFTER `type`')
    FROM `information_schema`.`COLUMNS`
    WHERE `TABLE_SCHEMA` = DATABASE() AND `TABLE_NAME` = 'moderation_tickets' AND `COLUMN_NAME` = 'category'
);
PREPARE stmt FROM @add_category;
EXECUTE stmt;
DEALLOCATE PREPARE stmt;

SET @add_reported_chats := (
    SELECT IF(COUNT(*) > 0,
        'DO 0',
        'ALTER TABLE `moderation_tickets` ADD COLUMN `reported_chats` text NULL DEFAULT NULL AFTER `message`')
    FROM `information_schema`.`COLUMNS`
    WHERE `TABLE_SCHEMA` = DATABASE() AND `TABLE_NAME` = 'moderation_tickets' AND `COLUMN_NAME` = 'reported_chats'
);
PREPARE stmt FROM @add_reported_chats;
EXECUTE stmt;
DEALLOCATE PREPARE stmt;

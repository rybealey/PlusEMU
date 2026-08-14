-- Moderation tickets are now persisted, so they survive an emulator restart.
-- Two fields on a ticket have nowhere to live in the shipped table:
--   category       the CFH topic the reporter picked. The existing `type`
--                  column holds the other category field, which is the one
--                  the client displays in the Open Issues list.
--   reported_chats the chat lines the reporter quoted as evidence, stored as
--                  a JSON array. Nullable: a NULL or unreadable value loads
--                  as no chats rather than failing startup.

-- Two separate statements, not one ALTER TABLE with both ADD COLUMNs: production's
-- table is migrated from Arcturus and not guaranteed to lack either column already
-- (some forks already carry `category`). One statement means either column already
-- existing fails the whole ALTER and creates neither; splitting lets a partially
-- present schema still pick up whichever column it's missing.
ALTER TABLE `moderation_tickets`
    ADD COLUMN `category` int(11) NOT NULL DEFAULT 0 AFTER `type`;

ALTER TABLE `moderation_tickets`
    ADD COLUMN `reported_chats` text NULL DEFAULT NULL AFTER `message`;

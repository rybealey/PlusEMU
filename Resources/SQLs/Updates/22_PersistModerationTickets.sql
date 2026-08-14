-- Moderation tickets are now persisted, so they survive an emulator restart.
-- Two fields on a ticket have nowhere to live in the shipped table:
--   category       the CFH topic the reporter picked. The existing `type`
--                  column holds the other category field, which is the one
--                  the client displays in the Open Issues list.
--   reported_chats the chat lines the reporter quoted as evidence, stored as
--                  a JSON array. Nullable: a NULL or unreadable value loads
--                  as no chats rather than failing startup.

ALTER TABLE `moderation_tickets`
    ADD COLUMN `category` int(11) NOT NULL DEFAULT 0 AFTER `type`,
    ADD COLUMN `reported_chats` text NULL DEFAULT NULL AFTER `message`;

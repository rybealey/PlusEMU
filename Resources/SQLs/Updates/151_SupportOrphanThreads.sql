-- pixelrp: clear the support threads that were created and then disowned.
--
-- StartThread inserted the thread, then asked for its id with a SEPARATE
-- `SELECT LAST_INSERT_ID()`. LAST_INSERT_ID is per-connection, and
-- IDatabase.Connection() hands back a CLOSED MySqlConnection - so Dapper
-- opened and closed it around every command, and the second call could be
-- served by a different pooled connection whose session had been reset. When
-- that happened the id came back 0: the row was in the table, the first
-- message was never attached to it, and the caller was told the start had
-- failed.
--
-- The visible result was a player being refused with "you already have a
-- conversation open" for conversations they could not see, because after two
-- of these their open count was genuinely at the cap. The rows also sit in the
-- staff queue with an empty preview and nothing to read.
--
-- A support thread with no messages cannot mean anything - nobody can answer
-- it and nobody wrote it - so they go. The code fix is in the same change;
-- this only cleans up after it.

DELETE t FROM `rp_support_threads` t
LEFT JOIN `rp_support_messages` m ON m.`thread_id` = t.`id`
WHERE m.`id` IS NULL;

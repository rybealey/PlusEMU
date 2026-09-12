-- pixelrp: the bank teller becomes a kind of bot, not just a costume.
--
-- 115 added the Bank Teller to the catalog as a bot preset with a figure and a
-- motto, and that is all it ever was: `ai_type` said 'generic', so a placed
-- teller was indistinguishable from any other bot standing behind a desk. The
-- `bots` table keeps no link back to the catalog row a bot came from, so there
-- was nothing to ask.
--
-- The emulator already had the answer and was not using it: BotUtility writes
-- `catalog_bot_presets`.`ai_type` straight into `bots`.`ai_type` on purchase,
-- and the AI type is what RoomBot.GenerateBotAi dispatches on. So the AI type
-- IS the identity - widen the enum, name the preset, and every teller bought
-- from now on knows what it is.

-- 1. The column has to allow the value before anything can carry it. 'banker'
--    goes last so the existing three keep their ordinals, which is what the
--    stored enum actually is on disk.
ALTER TABLE `bots`
    MODIFY COLUMN `ai_type` enum('generic','bartender','pet','banker')
    NOT NULL DEFAULT 'generic';

-- 2. Every Bank Teller bought from here on.
UPDATE `catalog_bot_presets` SET `ai_type` = 'banker' WHERE `id` = 107500;

-- 3. The one already standing in Mercury Bank, and any other bot placed from
--    this preset before today. Matched on the preset's own figure AND name,
--    both of which 115 wrote verbatim - a bot with that exact look and that
--    exact name came from this preset. A player who has renamed theirs is
--    missed and can be retagged by hand; a player who has neither renamed nor
--    redressed theirs gets a teller, which is what they bought.
UPDATE `bots` b
    JOIN `catalog_bot_presets` p ON p.`id` = 107500
    SET b.`ai_type` = 'banker'
    WHERE b.`ai_type` = 'generic'
      AND b.`name` = p.`name`
      AND b.`look` = p.`figure`;

-- What that touched, so a surprising number is visible in the deploy log
-- rather than discovered in somebody's bedroom. On beta this should be 1.
SELECT ROW_COUNT() AS tellers_retagged;

-- pixelrp: finish what 120 started - the PRESET's ai_type column.
--
-- 120 widened `bots`.`ai_type` to allow 'banker' and then set
-- `catalog_bot_presets`.`ai_type` to 'banker' as well. That second column is
-- its own enum - enum('pet','generic','bartender') - and 120 never touched it,
-- so the value was not valid there.
--
-- It did not fail. The server runs with `--sql-mode=NO_ENGINE_SUBSTITUTION`
-- (compose.yaml), which REPLACES the default mode rather than adding to it,
-- and so drops STRICT_TRANS_TABLES. Outside strict mode an invalid enum value
-- is coerced to the empty string and the statement reports success. The
-- migration looked clean and left the preset holding ''.
--
-- The cost was quiet and total: BotUtility copies the preset's ai_type into
-- every bot bought from it, and GetAiFromString falls through to Generic on
-- anything it does not recognise - so a Bank Teller bought from the catalog
-- was an ordinary bot wearing a teller's clothes. Existing tellers were
-- unaffected: 120's retag wrote to `bots`, whose column it had widened.

ALTER TABLE `catalog_bot_presets`
    MODIFY COLUMN `ai_type` enum('pet','generic','bartender','banker')
    NOT NULL DEFAULT 'generic';

UPDATE `catalog_bot_presets` SET `ai_type` = 'banker' WHERE `id` = 107500;

-- Anything else the same coercion emptied. There is no legitimate '' in this
-- column - the emulator reads it as Generic anyway, so making that explicit
-- costs nothing and leaves no blank rows to puzzle over later.
UPDATE `catalog_bot_presets` SET `ai_type` = 'generic' WHERE `ai_type` = '';

-- Should read 'banker'. If it reads anything else, the ALTER above did not
-- take and the same silent coercion is still in play.
SELECT `id`, `name`, `ai_type` FROM `catalog_bot_presets` WHERE `id` = 107500;

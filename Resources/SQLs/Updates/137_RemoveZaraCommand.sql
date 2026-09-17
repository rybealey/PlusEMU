-- pixelrp: :zara goes, now that a furni can open the Clothing Store.
--
-- The command was the only way in, so it existed. The zara_shop behaviour
-- replaces it with something better: a shop you walk into rather than a word
-- you type from anywhere in the hotel, which is the difference between a
-- roleplay hotel and a menu.
--
-- The permission row goes with the command. A row for a command that no longer
-- exists is harmless but misleading - the next person reading
-- permissions_commands should see what actually exists.
--
-- The window itself is untouched: RpOpenClothingStoreComposer and the whole
-- catalog_clothing shelf stay exactly as they were, and the furni behaviour
-- sends the same packet the command did.

DELETE FROM `permissions_commands` WHERE `command` = 'command_zara';

-- Should come back empty.
SELECT `command` FROM `permissions_commands` WHERE `command` = 'command_zara';

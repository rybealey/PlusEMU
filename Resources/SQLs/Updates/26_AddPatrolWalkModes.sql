-- Bot patrol walk modes: the rentable-bot menu's "Walk Horizontally" /
-- "Walk Vertically" (BotSkillSaveComposer action ids 90/91) pin a bot to a
-- back-and-forth patrol along one axis; "Walk freely" (92) returns it to
-- freeroam. Imported legacy rows can hold invalid '' enum values, so
-- normalize before narrowing the column through the new definition.
UPDATE `bots` SET `walk_mode` = 'freeroam' WHERE `walk_mode` NOT IN ('stand','freeroam','specified_range');
ALTER TABLE `bots` MODIFY `walk_mode` enum('stand','freeroam','specified_range','patrol_horizontal','patrol_vertical') NOT NULL DEFAULT 'freeroam';

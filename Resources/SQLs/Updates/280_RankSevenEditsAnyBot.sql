-- pixelrp: rank 7 (Community Leader) can edit any bot (Twist, 2026-10-19).
--
-- `bot_edit_any_override` is what lets a player rename, dress or set up a bot
-- they do not own - every bot in a PixelRP-owned room belongs to the PixelRP
-- account. The base dump gave it to ranks 8 (Developer) and 9 (Owner) only, so
-- a rename from rank 7 was dropped (SaveBotActionEvent, which now whispers
-- "You can't edit this bot." instead of saying nothing).
--
-- Rights here are NOT a threshold like permissions_commands.group_id: each rank
-- lists its own (GetPermissionsForPlayer looks up the exact rank). Rank 7 only,
-- as asked. Matched on the permission's NAME rather than its id. Takes effect
-- at each player's next login.
--
-- Idempotent: skipped when rank 7 already has the right.
SET @botedit := (SELECT `id` FROM `permissions` WHERE `permission` = 'bot_edit_any_override' LIMIT 1);

INSERT INTO `permissions_rights` (`group_id`, `permission_id`)
SELECT 7, @botedit
FROM DUAL
WHERE @botedit IS NOT NULL
  AND NOT EXISTS (SELECT 1 FROM `permissions_rights` `pr`
                  WHERE `pr`.`group_id` = 7 AND `pr`.`permission_id` = @botedit);

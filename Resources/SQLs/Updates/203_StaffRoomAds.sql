-- pixelrp: every staff rank edits room ads, not just 8 and 9.
--
-- `room_item_save_branding_items` is what lets a player set a room ad's image
-- and offsets. The base dump gave it to ranks 8 (Developer) and 9 (Owner)
-- only; this adds 5 (Administrator), 6 (Manager) and 7 (Community Leader).
--
-- Rights here are NOT a threshold like permissions_commands.group_id: each rank
-- lists its own (GetPermissionsForPlayer looks up the exact rank), so every
-- rank is named. Matched on the permission's NAME rather than its id 41.
--
-- The emulator half: Room.CanEditBranding lets a holder edit ads in any room
-- while clocked in at City Government, without the rest of room_any_owner -
-- which rank 5 does not hold. Takes effect at each player's next login.
--
-- Idempotent: a rank that already has the right is skipped.
SET @branding := (SELECT `id` FROM `permissions` WHERE `permission` = 'room_item_save_branding_items' LIMIT 1);

INSERT INTO `permissions_rights` (`group_id`, `permission_id`)
SELECT `r`.`rank`, @branding
FROM (SELECT 5 AS `rank` UNION ALL SELECT 6 UNION ALL SELECT 7) `r`
WHERE @branding IS NOT NULL
  AND NOT EXISTS (SELECT 1 FROM `permissions_rights` `pr`
                  WHERE `pr`.`group_id` = `r`.`rank` AND `pr`.`permission_id` = @branding);

-- pixelrp: gang roles are all real now (Gang window redesign, 2026-09-26).
--
-- Until now the leader (groups.owner_id) and "Member" (rp_gang_members.role_id
-- NULL) were IMPLICIT groups the client drew itself, and only custom roles had
-- rows. From here every member sits in a real rp_gang_roles row: a new gang
-- starts with one role called "Member" holding its founder, anyone can be
-- ranked into any role (the owner included - ownership is groups.owner_id,
-- not a role), new members join the BOTTOM role, and the last role can never
-- be deleted.
--
-- This moves existing gangs over without changing anyone's permissions:
--   1. every gang member gets a sidecar row if they somehow lack one;
--   2. every gang that has a role-less member, or no roles at all, gets a
--      "Member" role at the BOTTOM of its ladder, with no permissions -
--      exactly what role_id NULL meant before;
--   3. every role-less member (the owner included) is put in that role.
-- Custom roles, their order and their members are untouched.

INSERT IGNORE INTO `rp_gang_members` (`gang_id`, `user_id`, `role_id`, `joined_at`)
SELECT m.`group_id`, m.`user_id`, NULL, g.`created`
FROM `group_memberships` m
INNER JOIN `groups` g ON g.`id` = m.`group_id`
WHERE g.`is_gang` = '1';

INSERT INTO `rp_gang_roles` (`gang_id`, `name`, `sort_order`, `can_invite`, `can_kick`, `can_bank`, `is_admin`)
SELECT g.`id`, 'Member',
       COALESCE((SELECT MAX(r.`sort_order`) FROM `rp_gang_roles` r WHERE r.`gang_id` = g.`id`) + 1, 0),
       '0', '0', '0', '0'
FROM `groups` g
WHERE g.`is_gang` = '1'
  AND (EXISTS (SELECT 1 FROM `rp_gang_members` s WHERE s.`gang_id` = g.`id` AND s.`role_id` IS NULL)
       OR NOT EXISTS (SELECT 1 FROM `rp_gang_roles` r2 WHERE r2.`gang_id` = g.`id`));

-- the bottom role is the one with the highest sort_order (ties: newest id),
-- which for every gang touched above is the "Member" role just added
UPDATE `rp_gang_members` s
SET s.`role_id` = (
    SELECT r.`id` FROM `rp_gang_roles` r
    WHERE r.`gang_id` = s.`gang_id`
    ORDER BY r.`sort_order` DESC, r.`id` DESC
    LIMIT 1)
WHERE s.`role_id` IS NULL;

-- Renaming a gang from its Settings tab costs this many credits; the emulator
-- falls back to 100 when the row is missing or zero.
INSERT INTO `server_settings` (`key`, `value`, `description`)
VALUES ('gang.rename.cost', '100', 'pixelrp: credits charged to rename a gang from its Settings tab')
ON DUPLICATE KEY UPDATE `value` = `value`;

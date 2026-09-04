-- pixelrp: gangs-on-groups slice 3 - roster, roles, invites, level (see the
-- parent repo's docs/superpowers/specs/2026-09-04-gangs-on-groups-design.md).
-- Membership stays canonical in group_memberships; these tables are the
-- gang-only sidecars (role per member + join time, custom roles, invites).

ALTER TABLE `groups`
    ADD COLUMN `gang_level` int(11) NOT NULL DEFAULT 1 AFTER `is_gang`,
    ADD COLUMN `gang_xp` int(11) NOT NULL DEFAULT 0 AFTER `gang_level`;

-- Custom roles. The leader (groups.owner_id) is implicit and holds every
-- permission; members with role_id NULL are plain "Member" (no permissions).
-- is_admin implies invite + kick and unlocks role/member management.
CREATE TABLE IF NOT EXISTS `rp_gang_roles` (
  `id` int(11) NOT NULL AUTO_INCREMENT,
  `gang_id` int(11) unsigned NOT NULL,
  `name` varchar(29) NOT NULL,
  `sort_order` int(11) NOT NULL DEFAULT 0,
  `can_invite` enum('0','1') NOT NULL DEFAULT '0',
  `can_kick` enum('0','1') NOT NULL DEFAULT '0',
  `can_bank` enum('0','1') NOT NULL DEFAULT '0',
  `is_admin` enum('0','1') NOT NULL DEFAULT '0',
  PRIMARY KEY (`id`),
  KEY `gang` (`gang_id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

CREATE TABLE IF NOT EXISTS `rp_gang_members` (
  `gang_id` int(11) unsigned NOT NULL,
  `user_id` int(11) NOT NULL,
  `role_id` int(11) DEFAULT NULL,
  `joined_at` int(11) NOT NULL,
  PRIMARY KEY (`user_id`),
  KEY `gang` (`gang_id`),
  KEY `role` (`role_id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

CREATE TABLE IF NOT EXISTS `rp_gang_invites` (
  `id` int(11) NOT NULL AUTO_INCREMENT,
  `gang_id` int(11) unsigned NOT NULL,
  `user_id` int(11) NOT NULL,
  `invited_by` int(11) NOT NULL,
  `expires_at` int(11) NOT NULL,
  PRIMARY KEY (`id`),
  UNIQUE KEY `gang_user` (`gang_id`, `user_id`),
  KEY `user` (`user_id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

-- Founders of gangs created before this slice get a sidecar row dated to
-- the gang's creation so their roster shows a join date.
INSERT IGNORE INTO `rp_gang_members` (`gang_id`, `user_id`, `role_id`, `joined_at`)
SELECT m.`group_id`, m.`user_id`, NULL, g.`created`
FROM `group_memberships` m
INNER JOIN `groups` g ON g.`id` = m.`group_id`
WHERE g.`is_gang` = '1';

INSERT INTO `server_settings` (`key`, `value`, `description`)
VALUES ('gang.invite.hours', '24', 'pixelrp: hours before a pending gang invite expires')
ON DUPLICATE KEY UPDATE `value` = `value`;

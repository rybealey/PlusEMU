-- Corporation command keys (:superhire <user> <key> [rank] [tier]) and the
-- staff-only superhire command grant (rank 5+, same floor as :spawn).

ALTER TABLE `rp_corporations`
  ADD COLUMN `corp_key` varchar(24) NOT NULL DEFAULT '' AFTER `name`;

UPDATE `rp_corporations` SET `corp_key` = 'police' WHERE `id` = 1;

INSERT INTO `permissions_commands` (`command`, `group_id`, `subscription_id`)
VALUES ('command_superhire', 5, 0)
ON DUPLICATE KEY UPDATE `group_id` = 5;

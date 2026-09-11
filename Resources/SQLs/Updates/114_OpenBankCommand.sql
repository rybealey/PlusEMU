-- pixelrp: :openbank <username> - staff only, group_id 5 like the other
-- corporation and economy commands.
--
-- Accounts used to be opened from the Wallet's + menu. They are opened at a
-- BANK now, in person, and the bank is not built yet - so without this there
-- is no way to give anybody an account at all, and nothing downstream of one
-- can be tested.
--
-- It is a bridge, not a feature. When the branch exists, this stays as the
-- support tool for a player a teller could not help.
INSERT INTO `permissions_commands` (`command`, `group_id`, `subscription_id`)
VALUES ('command_openbank', 5, 0)
ON DUPLICATE KEY UPDATE `group_id` = 5;

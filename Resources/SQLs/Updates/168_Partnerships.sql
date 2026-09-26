-- PixelRP: formal partnerships, and relationships that are earned rather than
-- self-declared.
--
-- :propose puts a proposal on the other player's offer card; a yes partners
-- the two of them and each becomes the other's Love row on the profile.
-- :divorce ends it, from either side alone. See PartnershipUtility.
--
-- TWO ROWS PER COUPLE, (a,b) and (b,a): "who is X's partner" is then one
-- primary-key read from either side, and the primary key on user_id is what
-- makes a second partnership impossible. Both rows are written by one INSERT,
-- which InnoDB applies whole or not at all.
--
-- Not tied to the friends list: the stock relationship lives on
-- messenger_friendships, where unfriending would quietly divorce and a
-- stranger could never be married at all.

CREATE TABLE IF NOT EXISTS `rp_partnerships` (
    `user_id`    INT NOT NULL,
    `partner_id` INT NOT NULL,
    -- Unix seconds.
    `since`      INT NOT NULL,
    PRIMARY KEY (`user_id`),
    KEY `idx_partner` (`partner_id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

-- The avatar menu no longer lets anyone pin a heart, a smile or a skull on a
-- friend, so every one already pinned goes. The column stays - the friend list
-- packet still carries it - it simply reads 0 for everybody from here on.
UPDATE `messenger_friendships` SET `relationship` = 0 WHERE `relationship` <> 0;

INSERT INTO `permissions_commands` (`command`, `group_id`, `subscription_id`)
VALUES ('command_propose', 1, 0), ('command_divorce', 1, 0)
ON DUPLICATE KEY UPDATE `group_id` = 1;

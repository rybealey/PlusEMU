-- PixelRP VIP system: time-based single-tier membership bought with diamonds
-- via Store-tab tokens redeemed from the RP backpack. users.vip_expire (unix
-- seconds, 0 = never VIP) is the single source of truth; rank_vip is no
-- longer read. vip_last_stipend gates the daily diamond stipend to one grant
-- per calendar day. diamonds_store_items powers the in-game Store tab:
-- special_price, when non-NULL, overrides price and renders as a sale.
ALTER TABLE `users`
    ADD COLUMN `vip_expire` BIGINT NOT NULL DEFAULT 0 AFTER `rank_vip`,
    ADD COLUMN `vip_last_stipend` DATE NULL DEFAULT NULL AFTER `vip_expire`;

CREATE TABLE `diamonds_store_items` (
    `id` INT NOT NULL AUTO_INCREMENT,
    `item_key` VARCHAR(64) NOT NULL,
    `name` VARCHAR(128) NOT NULL,
    `description` VARCHAR(512) NOT NULL DEFAULT '',
    `icon` VARCHAR(64) NOT NULL,
    `price` INT NOT NULL,
    `special_price` INT NULL DEFAULT NULL,
    `vip_days` INT NOT NULL DEFAULT 0,
    `enabled` TINYINT(1) NOT NULL DEFAULT 1,
    `sort_order` INT NOT NULL DEFAULT 0,
    PRIMARY KEY (`id`),
    UNIQUE KEY `item_key` (`item_key`)
) ENGINE=InnoDB DEFAULT CHARSET=latin1;

CREATE TABLE `diamonds_store_purchases` (
    `id` INT NOT NULL AUTO_INCREMENT,
    `user_id` INT NOT NULL,
    `item_key` VARCHAR(64) NOT NULL,
    `diamonds_paid` INT NOT NULL,
    `created_at` TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    PRIMARY KEY (`id`),
    KEY `user_id` (`user_id`)
) ENGINE=InnoDB DEFAULT CHARSET=latin1;

INSERT INTO `diamonds_store_items`
    (`item_key`, `name`, `description`, `icon`, `price`, `special_price`, `vip_days`, `enabled`, `sort_order`)
VALUES
    ('vip_token_31', 'VIP Token (31 days)', 'Redeem from your backpack to activate 31 days of VIP.', 'vip-token-gold', 500, NULL, 31, 1, 1),
    ('vip_token_14', 'VIP Token (14 days)', 'Redeem from your backpack to activate 14 days of VIP.', 'vip-token-silver', 250, NULL, 14, 1, 2);

INSERT IGNORE INTO `server_settings` (`key`, `value`) VALUES ('vip.stipend.daily', '5');

-- The VIP badge must exist in badge_definitions or BadgeManager.GiveBadge
-- silently refuses to grant it.
INSERT IGNORE INTO `badge_definitions` (`code`, `required_right`) VALUES ('SVIP', '');

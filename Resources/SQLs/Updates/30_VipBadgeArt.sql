-- PixelRP VIP badge: subscriptions row 1 pointed at badge code SVIP, which has
-- no art in album1584. Point it at the stock VIP badge (VIP.gif exists) so the
-- redemption grant renders. SVIP can be restored later if custom art lands.
UPDATE `subscriptions` SET `badge_code` = 'VIP' WHERE `id` = 1;
INSERT IGNORE INTO `badge_definitions` (`code`, `required_right`) VALUES ('VIP', '');

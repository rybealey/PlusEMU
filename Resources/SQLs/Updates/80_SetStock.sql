-- pixelrp: :setstock <acronym> <quantity> - staff only, group_id 5 like the
-- other corporation commands (:corpsync, :superhire). Nothing in the game
-- writes rp_corporations.stock yet, so this is how the number moves and how
-- the phone's Stocks app can be seen working before farming and mining land.
-- Idempotent.
INSERT INTO `permissions_commands` (`command`, `group_id`, `subscription_id`)
VALUES ('command_setstock', 5, 0)
ON DUPLICATE KEY UPDATE `group_id` = 5;

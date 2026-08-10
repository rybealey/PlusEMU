-- Disable the passive currency drip: the per-player timer paid out credits
-- and pixels every 15 minutes just for being online, and because users.rank_vip
-- defaults to 1, every account also collected the Silver VIP subscription
-- bonus on top. Zero the base reward and every subscription bonus; the timer
-- still ticks but pays nothing.
UPDATE `server_settings` SET `value` = '0' WHERE `key` IN ('user.currency_scheduler.credit_reward', 'user.currency_scheduler.ducket_reward');
UPDATE `subscriptions` SET `credits` = 0, `duckets` = 0;

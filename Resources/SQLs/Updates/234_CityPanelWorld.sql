-- pixelrp: the City Panel's City tab (HabboHotel/CityPanel/CityWorld).
--
--   rp_city_world  the sky - holding the weather and pinning the time of day
--                  for everybody. Rank 5: it changes how the city looks, not
--                  who can play.
--   rp_city_hotel  maintenance (staff-only logins; everyone else is warned and
--                  taken offline a minute later) and the city-wide combat
--                  switch. Rank 8, like :ha - both reach every player at once.
--
-- The alert on the same tab follows the alert commands' own rows
-- (command_hotel_alert, command_staff_alert, command_room_alert).
--
-- The switches themselves are server_settings rows the panel writes
-- (hotel.maintenance, hotel.combat_paused, city.weather.*, city.time.*); a
-- missing row is the normal hotel, so nothing is seeded for them.
--
-- Idempotent: absolute assignments.

INSERT INTO `permissions_commands` (`command`, `group_id`, `subscription_id`)
VALUES ('rp_city_world', 5, 0)
ON DUPLICATE KEY UPDATE `group_id` = 5, `subscription_id` = 0;

INSERT INTO `permissions_commands` (`command`, `group_id`, `subscription_id`)
VALUES ('rp_city_hotel', 8, 0)
ON DUPLICATE KEY UPDATE `group_id` = 8, `subscription_id` = 0;

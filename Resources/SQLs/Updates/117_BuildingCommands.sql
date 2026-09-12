-- pixelrp: the building commands - :rot, :ss and :pickall.
--
-- Group 1, so every rank has them. They are not staff powers: each one checks
-- rights in the room it is used in and refuses out loud without them, so all
-- this row decides is whether the command exists for a player at all. A
-- builder given rights in somebody's room can use them there; nobody can use
-- them anywhere else.
--
-- :pickall already existed at group 1 in the original schema. It is re-stated
-- here so a hotel that tightened it by hand ends up in the same place as one
-- that did not.
INSERT INTO `permissions_commands` (`command`, `group_id`, `subscription_id`)
VALUES ('command_rot', 1, 0),
       ('command_ss', 1, 0),
       ('command_pickall', 1, 0)
ON DUPLICATE KEY UPDATE `group_id` = 1;

-- pixelrp: :undo - put the last piece of furni you moved, rotated, re-levelled
-- or faded back the way it was.
--
-- One step, and placement only. It cannot bring back furni that was picked up,
-- traded or deleted; the trash bin erases the row outright, and the command
-- says so rather than appearing to work.
--
-- Group 1, so everyone has it, on the same reasoning as :bh. The command holds
-- nothing but a snapshot of the builder's own last action, and putting furni
-- back requires rights in the room, which it checks and reports.
INSERT INTO `permissions_commands` (`command`, `group_id`, `subscription_id`)
VALUES ('command_undo', 1, 0)
ON DUPLICATE KEY UPDATE `group_id` = 1;

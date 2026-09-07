-- pixelrp police actions ported from the old Arcturus plugin: :stun, :cuff and
-- :escort, plus the two releases they need to be testable (:uncuff,
-- :unescort). group_id 1 = every player, on purpose and only for now - the
-- original gated all of these behind being a clocked-in police officer, and
-- that gate is deliberately not ported yet. Idempotent.
INSERT INTO `permissions_commands` (`command`, `group_id`, `subscription_id`)
VALUES ('command_stun', 1, 0), ('command_cuff', 1, 0), ('command_uncuff', 1, 0),
       ('command_escort', 1, 0), ('command_unescort', 1, 0)
ON DUPLICATE KEY UPDATE `group_id` = 1;

-- pixelrp: :bh <height> - a sticky build height.
--
-- Everything the builder places or drags sits at that height instead of
-- stacking on whatever is underneath. `:bh` on its own turns it back off.
--
-- Group 1, so everyone has it. There is nothing to gate: the command only
-- records a number against the user's own room session, and actually placing
-- furniture already requires rights in the room - which the command checks and
-- says out loud, rather than leaving a builder wondering why nothing moved.
INSERT INTO `permissions_commands` (`command`, `group_id`, `subscription_id`)
VALUES ('command_bh', 1, 0)
ON DUPLICATE KEY UPDATE `group_id` = 1;

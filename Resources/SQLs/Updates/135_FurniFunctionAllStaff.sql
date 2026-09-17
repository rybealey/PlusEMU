-- pixelrp: the Function tool opens to every staff member, rank 5 and up.
--
-- 103 registered `rp_furni_function` at 7. In permissions_commands, group_id is
-- a rank THRESHOLD, not a group: PermissionManager.GetCommandsForPlayer hands a
-- player every command where `player.Rank >= group_id`. So lowering the number
-- widens the gate, and 5 is this hotel's definition of staff - the same line
-- Habbo.IsStaff draws.
--
-- No code changes with it. Both packets (RpRequestFurniFunctionEvent and
-- RpFurniFunctionEvent) already check HasCommand, and the infostand button is
-- drawn from the same flag pushed by RpStaffDutyComposer, so the whole path
-- follows this row. That is the reason the tool was moved off a hardcoded rank
-- and onto a permission in the first place.
--
-- Two things this does NOT change:
--   * Being clocked in. The button still follows staff duty, so a rank-5 member
--     who is off duty still will not see it - that is deliberate and separate.
--   * The log. rp_furni_function_log keeps recording who changed what, which
--     matters more, not less, now that more people can.
--
-- Idempotent: an absolute assignment, so a re-run sets 5 to 5.

INSERT INTO `permissions_commands` (`command`, `group_id`, `subscription_id`)
VALUES ('rp_furni_function', 5, 0)
ON DUPLICATE KEY UPDATE `group_id` = 5, `subscription_id` = 0;

-- Should read 5. Anything else means the row is keyed differently than 103
-- wrote it and the INSERT added a second one instead of updating.
SELECT `command`, `group_id`, `subscription_id`
    FROM `permissions_commands`
    WHERE `command` = 'rp_furni_function';

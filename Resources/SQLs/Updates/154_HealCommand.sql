-- pixelrp: :heal.
--
-- Its own permission and not command_offer, because :heal is not an alias.
-- It does the offer's job for a clocked-in hospital employee and is going to
-- do other things for other people; a separate right is what lets those be
-- granted, revoked or priced separately later without touching :offer.
--
-- Without this row the command does not exist: CommandManager finds the key,
-- fails Permissions.HasCommand and returns false, which speaks the line to the
-- room as ordinary chat. See 153_OfferCommand.sql.
--
-- group_id 1 = every player. The gate is inside the command, and a civilian
-- who types it should be told what it wants rather than be met with silence.
INSERT INTO `permissions_commands` (`command`, `group_id`, `subscription_id`)
VALUES ('command_heal', 1, 0)
ON DUPLICATE KEY UPDATE `group_id` = 1;

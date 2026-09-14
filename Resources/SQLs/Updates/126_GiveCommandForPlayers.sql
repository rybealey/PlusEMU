-- pixelrp: :give becomes a player's command.
--
-- It was staff-only (group 2) and minted currency out of nothing. It also did
-- not work: the command manager strips the username before calling a target
-- command, so the old reads of parameters[1] and [2] landed one slot past the
-- currency and the amount and every invocation answered "'10' is not a valid
-- currency". Nothing is being taken away that anybody could use.
--
-- Now it moves money from the giver's purse to the target's, in the same room,
-- announced in the room. So it belongs to everyone, which is group 1 - the
-- same group :passive and the other player commands sit in (63_PassiveCommand).
--
-- Minting still exists where it belongs: the RCON give_user_currency command,
-- which housekeeping reaches and which no player can type.

INSERT INTO `permissions_commands` (`command`, `group_id`, `subscription_id`)
VALUES ('command_give', 1, 0)
ON DUPLICATE KEY UPDATE `group_id` = 1;

-- The per-currency staff gates the old command checked. Nothing reads them any
-- more - a player gives their own money and needs no permission to do it - and
-- leaving them would suggest a staff route that no longer exists.
DELETE FROM `permissions_commands`
    WHERE `command` IN ('command_give_coins', 'command_give_pixels',
                        'command_give_diamonds', 'command_give_gotw');

-- command_give_badge is a DIFFERENT command (GiveBadgeCommand) and stays.
SELECT `command`, `group_id` FROM `permissions_commands`
    WHERE `command` LIKE 'command\_give%' ORDER BY `command`;

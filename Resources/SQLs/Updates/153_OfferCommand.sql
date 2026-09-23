-- pixelrp: :offer and its alias :sell.
--
-- WITHOUT THIS ROW THE COMMAND DOES NOT EXIST. CommandManager looks the key
-- up, finds it, asks Permissions.HasCommand for the class's
-- PermissionRequired, and on a miss returns false - which sends the line to
-- the room as ordinary chat. So a command with no grant does not refuse
-- loudly; it just says ":offer twist medkit" out loud in front of everybody.
--
-- group_id 1 = every player, and deliberately so. The real gate is inside the
-- command: on-duty hospital staff holding the right handitem. Gating the
-- PERMISSION as well would mean a civilian who tries it gets no answer at all,
-- where what they should get is "Only hospital staff can sell that." - which
-- tells them the command is real and what it wants.
--
-- One permission covers both words: SellCommand inherits PermissionRequired
-- from OfferCommand, so :sell is the same right under another key.
--
-- Idempotent, in the shape 78_CopCommands.sql already uses.
INSERT INTO `permissions_commands` (`command`, `group_id`, `subscription_id`)
VALUES ('command_offer', 1, 0)
ON DUPLICATE KEY UPDATE `group_id` = 1;

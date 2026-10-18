-- pixelrp: the chat commands clean-up (Twist, 2026-10-18).
--
-- Every chat command was gone through, category by category. This is the
-- database half; the command classes removed in the same commit are the rest.
-- Commands are found by assembly scan, so a deleted class is gone from the
-- hotel - these rows are what would otherwise be left behind.
--
-- Idempotent: the deletes find nothing the second time, and the staff rows are
-- an upsert.

-- 1. Permissions of the commands deleted in this commit: :about (command_info),
--    :convertcredits, :emptyitems, :setmax, :disablegifts, :room, :pet,
--    :moonwalk, :forced_effects, :superfastwalk and :deletegroup. (:sellroom
--    and :eha never had a row - nobody could use them - and :forceredirect
--    shared command_update, which :update and :movementstats still use.)
DELETE FROM `permissions_commands` WHERE `command` IN (
    'command_info',
    'command_convert_credits',
    'command_empty_items',
    'command_setmax',
    'command_disable_gifts',
    'command_room',
    'command_pet',
    'command_moonwalk',
    'command_forced_effects',
    'command_super_fastwalk',
    'command_delete_group'
);

-- 2. 19 permissions nothing uses: no command, emulator check, client or CMS
--    refers to them. Most are the stock emulator's, for commands this hotel
--    never had; command_hug, command_kiss and command_bite were left behind
--    when those three were opened to everybody. A command unlock
--    (user_command_unlocks) only counts while its name is still here, so any
--    unlock of these goes with them.
DELETE FROM `permissions_commands` WHERE `command` IN (
    'command_bite',
    'command_clubnx',
    'command_control',
    'command_debug',
    'command_empty',
    'command_force_draw',
    'command_hug',
    'command_hvusers',
    'command_kiss',
    'command_maintenance',
    'command_make_say',
    'command_make_shout',
    'command_rig',
    'command_rights',
    'command_room_say',
    'command_room_shout',
    'command_transfer',
    'command_update_cata_full',
    'command_update_tickets'
);

-- 3. Staff only - every staff rank, Trial Moderator (2) and up - with no VIP
--    route: :ejectall, :kickbots, :disablediagonal (was Admin+), :unload,
--    :enable, :faceless, :follow, :mimic (was VIP), :spit (was Admin+; a Spit
--    Token still unlocks it for one player), :spush (was VIP) and :spull (was
--    VIP).
INSERT INTO `permissions_commands` (`command`, `group_id`, `subscription_id`) VALUES
    ('command_ejectall', 2, 0),
    ('command_kickbots', 2, 0),
    ('command_disable_diagonal', 2, 0),
    ('command_unload', 2, 0),
    ('command_enable', 2, 0),
    ('command_faceless', 2, 0),
    ('command_follow', 2, 0),
    ('command_mimic', 2, 0),
    ('command_spit', 2, 0),
    ('command_super_push', 2, 0),
    ('command_super_pull', 2, 0)
ON DUPLICATE KEY UPDATE `group_id` = 2, `subscription_id` = 0;

-- Should list only the eleven staff rows, each at 2 / 0.
SELECT `command`, `group_id`, `subscription_id` FROM `permissions_commands`
WHERE `command` IN (
    'command_info', 'command_convert_credits', 'command_empty_items', 'command_setmax',
    'command_disable_gifts', 'command_room', 'command_pet', 'command_moonwalk',
    'command_forced_effects', 'command_super_fastwalk', 'command_delete_group',
    'command_bite', 'command_hug', 'command_kiss',
    'command_ejectall', 'command_kickbots', 'command_disable_diagonal', 'command_unload',
    'command_enable', 'command_faceless', 'command_follow', 'command_mimic',
    'command_spit', 'command_super_push', 'command_super_pull'
);

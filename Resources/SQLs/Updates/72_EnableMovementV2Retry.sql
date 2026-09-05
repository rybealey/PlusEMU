-- pixelrp Movement V2: re-enable for the second beta test.
--
-- The first test (70) froze avatars on their first step: the movement-critical
-- tile barrier was armed at PLAN time, so the scheduler's pre-commit check
-- blocked the very AdvanceWalker call that would have queued the tile event and
-- cleared it. Blocked walkers were also left due in the past, so the room stayed
-- permanently due and the scheduler busy-looped on the room lock, which is what
-- froze clients rather than just avatars.
--
-- Both are fixed (emulator d711fe44): the barrier is not armed at all in this
-- build - tile effects already run inline in ApplyMovementV2Frame under
-- _cycleLock - and a blocked walker is now deferred rather than left hot-due.
--
-- From here on, prefer the in-game toggle over another SQL patch:
--   :movementv2 on
--   :movementv2 off
-- It writes this same row and reloads the settings cache, so it takes effect
-- immediately and rollback no longer needs a deploy.

INSERT INTO `server_settings` (`key`, `value`, `description`) VALUES
('movement.v2.enabled', '1', 'Movement V2: 1 = V2 owns route + timing for human users (bots/pets stay on V1). Anything else = off. Toggle live with :movementv2 on|off.')
ON DUPLICATE KEY UPDATE `value` = '1';

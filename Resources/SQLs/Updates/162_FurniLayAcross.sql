-- PixelRP: a laying furni (bed, tent, medical bed) can have avatars lie ACROSS
-- it instead of along it, set per furni in the Function tool.
--
-- Why a switch and not a rotation: the client can draw a lying avatar only two
-- ways. nitro-renderer's AvatarImage turns the lay posture's direction 0 into 4
-- and every other direction into 2, so four "lay directions" would collapse
-- onto two drawings anyway. 0 keeps today's behaviour for every furni.

ALTER TABLE `furniture`
    ADD COLUMN `lay_across` TINYINT(1) NOT NULL DEFAULT 0 AFTER `height_marker`;

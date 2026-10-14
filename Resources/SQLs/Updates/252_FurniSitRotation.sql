-- PixelRP: which way an avatar faces when it sits on a furni, set per furni in
-- the Function tool ("Sit facing").
--
-- Stored as a turn ADDED to the furni's own rotation - 0, 2, 4 or 6, each step
-- of 2 a quarter-turn to the right - so the sitter turns with the furni when it
-- is rotated. For furni whose artwork faces a different way from its rotation
-- number, where everyone sits sideways or backwards. 0 keeps today's behaviour
-- for every furni.
--
-- INT rather than TINYINT(1), which the driver hands back as a bool (see 231).

ALTER TABLE `furniture`
    ADD COLUMN `sit_rotation` INT NOT NULL DEFAULT 0 AFTER `lay_across`;

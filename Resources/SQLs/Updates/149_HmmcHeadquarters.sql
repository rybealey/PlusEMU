-- pixelrp: room 4 is the Harvey Milk Medical Center's headquarters.
--
-- `rooms.corporation_id` (0 = not an HQ, from 53) is what makes a room a
-- corporation's HQ. It decides where a shift can be clocked in, and since the
-- hospital admission feature it also decides where an uncollected casualty is
-- taken: HospitalAdmission looks up the one room belonging to a corporation
-- whose service_type is 'medical', and does nothing at all if there is not
-- one. Until this file there was no such room, so that half of the feature was
-- inert.
--
-- THE ONLY ONE, which is why the first statement exists. The lookup takes the
-- lowest-id match, so a second medical HQ would not be an error - it would
-- just quietly win or lose depending on its id, and a casualty would be
-- delivered somewhere nobody is expecting them. Clearing the others makes
-- "room 4 is the hospital" true rather than merely likely.
--
-- HMMC is resolved by ACRONYM, not id: corporation ids differ between beta and
-- prod, and HMMC is seeded by hand on beta only (see 54). Where it does not
-- exist @hmmc is NULL, every statement below matches nothing, and this file is
-- a no-op - which is the correct behaviour on a database with no hospital.
--
-- Room 4 itself is not created or checked here. If it does not exist the
-- UPDATE touches no rows and the hospital simply has no HQ, exactly as before.
--
-- NOT PICKED UP BY A ROOM ALREADY IN MEMORY: RoomFactory reads
-- corporation_id when a room loads, so a loaded room 4 keeps its old value
-- until it unloads. The admission path is unaffected - it queries the database
-- directly - but a shift clock-in may need the room to empty first.
--
-- Idempotent: absolute assignments scoped by id, so a re-run sets the same
-- values.

SET @hmmc := (SELECT `id` FROM `rp_corporations` WHERE `acronym` = 'HMMC' LIMIT 1);

-- Any other room claiming to be the hospital stops being one.
UPDATE `rooms`
    SET `corporation_id` = 0
    WHERE @hmmc IS NOT NULL
      AND `corporation_id` = @hmmc
      AND `id` <> 4;

-- Room 4 is the hospital.
UPDATE `rooms`
    SET `corporation_id` = @hmmc
    WHERE @hmmc IS NOT NULL
      AND `id` = 4;

-- Should return exactly one row: room 4, with HMMC's id.
SELECT `r`.`id`, `r`.`caption`, `r`.`corporation_id`, `c`.`acronym`, `c`.`service_type`
    FROM `rooms` `r`
    JOIN `rp_corporations` `c` ON `c`.`id` = `r`.`corporation_id`
    WHERE `c`.`service_type` = 'medical';

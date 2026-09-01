-- pixelrp: rename two Harvey Milk Medical Center (HMMC) ranks.
--   'Doctor'  -> 'Surgeon'
--   'Surgeon' (the higher rung above Doctor) -> 'Paramedic'
-- Order matters: rename the original higher 'Surgeon' to 'Paramedic' FIRST,
-- so the second statement renaming 'Doctor' to 'Surgeon' can't collide with
-- a still-named 'Surgeon' row. Scoped to HMMC by acronym so no other corp's
-- ranks are touched. Runs exactly once (recorded in _applied_sql_updates);
-- a no-op on any DB where HMMC isn't seeded yet (e.g. prod - HMMC is
-- beta-only, and its eventual prod seed must use these renamed values).

UPDATE `rp_corporation_ranks` r
  INNER JOIN `rp_corporations` c ON c.`id` = r.`corporation_id`
  SET r.`name` = 'Paramedic'
  WHERE c.`acronym` = 'HMMC' AND r.`name` = 'Surgeon';

UPDATE `rp_corporation_ranks` r
  INNER JOIN `rp_corporations` c ON c.`id` = r.`corporation_id`
  SET r.`name` = 'Surgeon'
  WHERE c.`acronym` = 'HMMC' AND r.`name` = 'Doctor';

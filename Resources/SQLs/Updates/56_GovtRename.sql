-- pixelrp: rename the PixelRP Leadership corporation to City Government,
-- acronym PRPL -> GOVT. Beta-only corp (corp id 5); runs once (recorded in
-- _applied_sql_updates), a no-op on any DB where it isn't seeded. service_type
-- is untouched (stays 'staff'), so City Government keeps its work-anywhere
-- access. Staff commands now use the GOVT acronym (:superhire <user> GOVT ...).

UPDATE `rp_corporations`
  SET `name` = 'City Government', `acronym` = 'GOVT'
  WHERE `acronym` = 'PRPL';

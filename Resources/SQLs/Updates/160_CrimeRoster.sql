-- pixelrp: the charge list, replaced wholesale.
--
-- Fifteen charges, from the list the team drew up. Each row is the crime an
-- officer can file with `:charge <player> <key>`, its sentence, and - where it
-- can be settled - the ticket that settles it instead (159_CrimeTickets).
--
-- SEVERITY is not on the list, so it is derived, and derived one way: from
-- the jail time, in roughly doubling bands. The list pairs every ticket with a
-- sentence in fixed steps ($8/3m, $16/6m, $24/9m, $60/15m, $100/30m), so the
-- sentence already says how serious the team thinks a crime is - and it is
-- the one number every charge has (Logout is not ticketable).
--   jail <= 3m -> 1 star    <= 6m -> 2    <= 10m -> 3    <= 20m -> 4    else 5
--
-- AUTO charges (AC) are meant to be filed by the system - logging out while
-- wanted, a kill - and manual ones (MC) by an officer. Nothing files charges
-- automatically yet, so for now the description records which is which and
-- every charge can be filed by hand. Frisk markings are deliberately ignored.
--
-- A crime's `key_name` is unique, so a charge that already existed under the
-- same key (assault, robbery, trespass, murder) is updated in place and keeps
-- its id - and with it any rap sheet that names it.

INSERT INTO `rp_crimes`
  (`key_name`, `name`, `description`, `jail_seconds`, `severity`, `stackable`, `ticketable`, `ticket_amount`, `active`, `sort_order`)
VALUES
  ('execution', 'Execution', 'Auto charge - filed by the system.', 540, 3, 1, 1, 24, 1, 1),
  ('logout', 'Logout', 'Auto charge - filed by the system.', 1800, 5, 0, 0, 0, 1, 2),
  ('obstruction', 'Obstruction', 'Manual charge - filed by an officer.', 180, 1, 0, 1, 8, 1, 3),
  ('copassault', 'Cop Assault', 'Manual charge - filed by an officer.', 360, 2, 1, 1, 16, 1, 4),
  ('robbery', 'Robbery', 'Auto charge - filed by the system.', 540, 3, 0, 1, 24, 1, 5),
  ('trespass', 'Trespass', 'Manual charge - filed by an officer.', 180, 1, 0, 1, 8, 1, 6),
  ('drugs', 'Drugs', 'Manual charge - filed by an officer.', 360, 2, 0, 1, 16, 1, 7),
  ('copmurder', 'Cop Murder', 'Auto charge - filed by the system.', 900, 4, 1, 1, 60, 1, 8),
  ('murder', 'Murder', 'Auto charge - filed by the system.', 360, 2, 1, 1, 16, 1, 9),
  ('911abuse', '911 Abuse', 'Manual charge - filed by an officer.', 180, 1, 1, 1, 8, 1, 10),
  ('jailbreak', 'Jailbreak', '', 1800, 5, 1, 1, 100, 1, 11),
  ('ganghomicide', 'Gang Homicide', 'Auto charge - filed by the system.', 180, 1, 1, 1, 8, 1, 12),
  ('assault', 'Assault', 'Manual charge - filed by an officer.', 180, 1, 1, 1, 8, 1, 13),
  ('terrorism', 'Terrorism', 'Manual charge - filed by an officer.', 360, 2, 0, 1, 16, 1, 14),
  ('paramurder', 'Paramedic Murder', 'Auto charge - filed by the system.', 540, 3, 0, 1, 24, 1, 15)
ON DUPLICATE KEY UPDATE
  `name` = VALUES(`name`), `description` = VALUES(`description`), `jail_seconds` = VALUES(`jail_seconds`),
  `severity` = VALUES(`severity`), `stackable` = VALUES(`stackable`), `ticketable` = VALUES(`ticketable`),
  `ticket_amount` = VALUES(`ticket_amount`), `active` = VALUES(`active`), `sort_order` = VALUES(`sort_order`);

-- Everything else goes. A crime nobody has ever been charged with is deleted;
-- one that appears on any rap sheet - open or dropped - is retired instead, so
-- the sheet can still name what it was for. The same rule housekeeping's own
-- delete button follows.
DELETE c FROM `rp_crimes` c
  LEFT JOIN `rp_charges` ch ON ch.`crime_id` = c.`id`
 WHERE c.`key_name` NOT IN ('execution', 'logout', 'obstruction', 'copassault', 'robbery', 'trespass', 'drugs', 'copmurder', 'murder', '911abuse', 'jailbreak', 'ganghomicide', 'assault', 'terrorism', 'paramurder')
   AND ch.`id` IS NULL;

UPDATE `rp_crimes` SET `active` = 0 WHERE `key_name` NOT IN ('execution', 'logout', 'obstruction', 'copassault', 'robbery', 'trespass', 'drugs', 'copmurder', 'murder', '911abuse', 'jailbreak', 'ganghomicide', 'assault', 'terrorism', 'paramurder');

-- Receipt: the fifteen, and anything kept only for history.
SELECT CONCAT('charges on the list: ', COUNT(*)) AS `receipt` FROM `rp_crimes` WHERE `active` = 1;
SELECT CONCAT('retired for history: ', COUNT(*)) AS `receipt` FROM `rp_crimes` WHERE `active` = 0;

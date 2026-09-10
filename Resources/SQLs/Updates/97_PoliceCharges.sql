-- pixelrp: police powers are a job, and crimes are data.
--
-- Three things:
--   1. a flag saying which corporation IS the police, so :stun, :cuff and
--      :escort can be gated on employment rather than on everyone;
--   2. the crimes a player can be charged with, managed in /housekeeping;
--   3. the charges themselves - who charged whom, with what, and when.

-- 1. Which corporation is the police force.
--
-- A flag rather than the hardcoded id 1: the force can be renamed, a second
-- one can exist (a sheriff's office, a federal agency), and housekeeping can
-- move it without a migration. Nothing assumes exactly one row carries it.
ALTER TABLE `rp_corporations`
    ADD COLUMN `is_police` TINYINT(1) NOT NULL DEFAULT 0 AFTER `manage_rank_order`;

UPDATE `rp_corporations` SET `is_police` = 1
    WHERE `id` = 1 OR `name` = 'San Francisco Police Department';

-- 2. The crimes.
--
-- `key_name` is what an officer types - `:charge Yavn gta` - so it is short,
-- lowercase and unique. The display name is what appears on a rap sheet.
--
-- `jail_seconds` is carried here rather than applied here: nothing in the
-- hotel arrests anyone yet. It is the sentence this crime WOULD attract, so
-- the number is ready the day arrest lands, and 0 means "no custodial time".
--
-- `stackable` = whether the same crime can sit on a player's sheet more than
-- once at a time. Assault twice in an evening is two counts; being an
-- unlicensed driver is a state, not a tally.
CREATE TABLE IF NOT EXISTS `rp_crimes` (
  `id` INT NOT NULL AUTO_INCREMENT,
  `key_name` VARCHAR(24) NOT NULL,
  `name` VARCHAR(64) NOT NULL,
  `description` VARCHAR(255) NOT NULL DEFAULT '',
  `jail_seconds` INT NOT NULL DEFAULT 0,
  `stackable` TINYINT(1) NOT NULL DEFAULT 1,
  -- a retired crime keeps its history but can no longer be charged
  `active` TINYINT(1) NOT NULL DEFAULT 1,
  `sort_order` INT NOT NULL DEFAULT 0,
  PRIMARY KEY (`id`),
  UNIQUE KEY `uniq_crime_key` (`key_name`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

-- 3. The charges. One row per count, never deleted by charging again - a rap
-- sheet is a history, so a dropped charge is a `dropped_at`, not a DELETE.
CREATE TABLE IF NOT EXISTS `rp_charges` (
  `id` INT NOT NULL AUTO_INCREMENT,
  `user_id` INT NOT NULL,
  `crime_id` INT NOT NULL,
  -- who filed it; kept even if they later leave the force
  `officer_id` INT NOT NULL,
  `charged_at` INT NOT NULL DEFAULT 0,
  `dropped_at` INT NOT NULL DEFAULT 0,
  PRIMARY KEY (`id`),
  KEY `idx_charges_user` (`user_id`, `dropped_at`),
  KEY `idx_charges_crime` (`crime_id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

-- A starter set, so the command has something to charge on day one and the
-- housekeeping table is not an empty page. Every one of these is editable.
INSERT INTO `rp_crimes` (`key_name`, `name`, `description`, `jail_seconds`, `stackable`, `sort_order`)
VALUES
  ('assault',    'Assault',                'Attacking another citizen.',                       300, 1, 1),
  ('battery',    'Aggravated Battery',     'Attacking another citizen with a weapon.',         600, 1, 2),
  ('theft',      'Petty Theft',            'Taking property worth little.',                    180, 1, 3),
  ('gta',        'Grand Theft Auto',       'Taking a vehicle that is not yours.',              600, 1, 4),
  ('robbery',    'Armed Robbery',          'Taking property by force or threat.',              900, 1, 5),
  ('trespass',   'Trespassing',            'Being somewhere you have been told not to be.',    120, 1, 6),
  ('resisting',  'Resisting Arrest',       'Fleeing or fighting a lawful arrest.',             300, 1, 7),
  ('obstruction','Obstruction of Justice', 'Interfering with an officer at work.',             300, 1, 8),
  ('possession', 'Possession',             'Carrying a controlled substance.',                 240, 1, 9),
  ('dealing',    'Dealing',                'Selling a controlled substance.',                  900, 1, 10),
  ('speeding',   'Speeding',               'Driving well over the limit.',                       0, 1, 11),
  ('unlicensed', 'Driving Unlicensed',     'Driving without a licence. A state, not a tally.',   0, 0, 12),
  ('disorder',   'Disorderly Conduct',     'Causing a scene in public.',                        120, 1, 13),
  ('murder',     'Murder',                 'Killing another citizen.',                         3600, 1, 14)
ON DUPLICATE KEY UPDATE `key_name` = `key_name`;

-- 4. :charge needs a permission row like the other police commands. group_id 1
--    is every player ON PURPOSE: the real gate is the on-duty employment check
--    in PoliceUtility, not a staff rank, and the same is true of :stun, :cuff
--    and :escort now. 78_CopCommands says that gate "is deliberately not ported
--    yet" - as of this migration it is, for those three and for this one.
INSERT INTO `permissions_commands` (`command`, `group_id`, `subscription_id`)
VALUES ('command_charge', 1, 0)
ON DUPLICATE KEY UPDATE `group_id` = 1;

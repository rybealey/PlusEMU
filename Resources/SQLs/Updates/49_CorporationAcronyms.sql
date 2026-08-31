-- Corporation acronyms: the short form used on compact surfaces (today:
-- the [WORKING] motto). '' falls back to the full name server-side.

ALTER TABLE `rp_corporations`
  ADD COLUMN `acronym` varchar(12) NOT NULL DEFAULT '' AFTER `name`;

UPDATE `rp_corporations` SET `acronym` = 'SFPD' WHERE `id` = 1;

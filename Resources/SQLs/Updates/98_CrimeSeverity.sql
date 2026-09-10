-- pixelrp: how badly wanted a crime makes you.
--
-- 1-5, the same scale the HUD's wanted stars and RpWantedView's WantedEntry
-- already use, so a severity IS a star count rather than something that has to
-- be mapped onto one.
--
-- THE RULE, written down here because the column alone does not say it: a
-- player's wanted level is the HIGHEST severity among their open charges, not
-- the sum. Five speeding tickets are not a murder. Escalation - counts adding
-- up within a severity - is a deliberate later decision, and changing this
-- rule is a change to one query rather than to the data.
--
-- Nothing reads this yet: the HUD's stars still come from a username hash
-- (mockStatsFor in PlayerHudWidgetView) and RpWantedView renders an empty
-- list, because there is no packet carrying charges to the client. This is the
-- number that feed will serve when it exists.
ALTER TABLE `rp_crimes`
    ADD COLUMN `severity` TINYINT NOT NULL DEFAULT 1 AFTER `jail_seconds`;

-- The starter set, graded. Anything not listed keeps the default 1.
UPDATE `rp_crimes` SET `severity` = 5 WHERE `key_name` IN ('murder');
UPDATE `rp_crimes` SET `severity` = 4 WHERE `key_name` IN ('robbery', 'battery');
UPDATE `rp_crimes` SET `severity` = 3 WHERE `key_name` IN ('gta', 'dealing', 'assault');
UPDATE `rp_crimes` SET `severity` = 2 WHERE `key_name` IN ('theft', 'possession', 'resisting', 'obstruction');
UPDATE `rp_crimes` SET `severity` = 1 WHERE `key_name` IN ('trespass', 'speeding', 'unlicensed', 'disorder');

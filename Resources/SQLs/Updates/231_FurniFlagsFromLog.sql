-- pixelrp: Height marker and Lie across put back to what staff last chose.
--
-- The emulator read both columns wrong on load. They are TINYINT(1), which the
-- driver returns as a bool, and ItemDataManager compared the stringified
-- "True"/"False" with "1" and "0" - so after every restart each furni's Height
-- marker read ON and each Lie across read OFF, whatever was stored. That is
-- fixed in the same change as this file.
--
-- The column values were damaged too. The Function tool sends every field on
-- every save, so saving ANY change to a furni hotel-wide (its name, a
-- behaviour) wrote back the wrongly loaded pair: height_marker = 1, and
-- lay_across = 0. No log row was written for those two, because the tool saw
-- no change in them.
--
-- A deliberate choice always IS logged (rp_furni_function_log, field
-- 'height_marker' / 'lay_across', new_value '1' or '0'). So each furni gets the
-- value of its most recent logged choice, and one never chosen gets the
-- default: marker off (112), lying along (162).
--
-- Idempotent.

UPDATE `furniture` f
LEFT JOIN (
    SELECT l.`definition_id`, l.`new_value`
    FROM `rp_furni_function_log` l
    JOIN (
        SELECT `definition_id`, MAX(`id`) AS `id`
        FROM `rp_furni_function_log`
        WHERE `field` = 'height_marker'
        GROUP BY `definition_id`
    ) latest ON latest.`id` = l.`id`
) chosen ON chosen.`definition_id` = f.`id`
SET f.`height_marker` = IF(chosen.`new_value` = '1', 1, 0);

UPDATE `furniture` f
LEFT JOIN (
    SELECT l.`definition_id`, l.`new_value`
    FROM `rp_furni_function_log` l
    JOIN (
        SELECT `definition_id`, MAX(`id`) AS `id`
        FROM `rp_furni_function_log`
        WHERE `field` = 'lay_across'
        GROUP BY `definition_id`
    ) latest ON latest.`id` = l.`id`
) chosen ON chosen.`definition_id` = f.`id`
SET f.`lay_across` = IF(chosen.`new_value` = '1', 1, 0);

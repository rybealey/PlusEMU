-- pixelrp: the Habzone clothing out of every player's hands too. 267 took it
-- off the shelf and kept what players had bought; now it goes entirely: the
-- owned pieces (user_clothing) and every stored look still wearing one of its
-- sets (400000-401792). A look that wore an imported face gets the default
-- face for its gender back rather than no head; anything else is just dropped.
-- Logged-in looks already lose unknown sets in ProcessFigure; this covers
-- saved outfits, bots, uniforms and players who have not logged in since.

DELETE FROM `user_clothing`
 WHERE `part_id` REGEXP '^[0-9]+$'
   AND CAST(`part_id` AS UNSIGNED) BETWEEN 400000 AND 401792;

-- a set of the block in a figure: .<type>-<400000..401799>[-colour...], the
-- dots kept by the lookahead so neighbouring parts still split
UPDATE `users`
   SET `look` = TRIM(BOTH '.' FROM REGEXP_REPLACE(
         REGEXP_REPLACE(CONCAT('.', `look`, '.'),
           '\\.hd-40(0[0-9]{3}|1[0-7][0-9]{2})(-[0-9]*)*(?=\\.)', IF(`gender` = 'F', '.hd-600-1', '.hd-180-1')),
         '\\.[a-z]{2,3}-40(0[0-9]{3}|1[0-7][0-9]{2})(-[0-9]*)*(?=\\.)', ''))
 WHERE `look` REGEXP '(^|\\.)[a-z]{2,3}-40(0[0-9]{3}|1[0-7][0-9]{2})(-|\\.|$)';

UPDATE `user_wardrobe`
   SET `look` = TRIM(BOTH '.' FROM REGEXP_REPLACE(
         REGEXP_REPLACE(CONCAT('.', `look`, '.'),
           '\\.hd-40(0[0-9]{3}|1[0-7][0-9]{2})(-[0-9]*)*(?=\\.)', IF(`gender` = 'F', '.hd-600-1', '.hd-180-1')),
         '\\.[a-z]{2,3}-40(0[0-9]{3}|1[0-7][0-9]{2})(-[0-9]*)*(?=\\.)', ''))
 WHERE `look` REGEXP '(^|\\.)[a-z]{2,3}-40(0[0-9]{3}|1[0-7][0-9]{2})(-|\\.|$)';

UPDATE `bots`
   SET `look` = TRIM(BOTH '.' FROM REGEXP_REPLACE(
         REGEXP_REPLACE(CONCAT('.', `look`, '.'),
           '\\.hd-40(0[0-9]{3}|1[0-7][0-9]{2})(-[0-9]*)*(?=\\.)', IF(`gender` = 'F', '.hd-600-1', '.hd-180-1')),
         '\\.[a-z]{2,3}-40(0[0-9]{3}|1[0-7][0-9]{2})(-[0-9]*)*(?=\\.)', ''))
 WHERE `look` REGEXP '(^|\\.)[a-z]{2,3}-40(0[0-9]{3}|1[0-7][0-9]{2})(-|\\.|$)';

UPDATE `catalog_bot_presets`
   SET `figure` = TRIM(BOTH '.' FROM REGEXP_REPLACE(
         REGEXP_REPLACE(CONCAT('.', `figure`, '.'),
           '\\.hd-40(0[0-9]{3}|1[0-7][0-9]{2})(-[0-9]*)*(?=\\.)', IF(`gender` = 'F', '.hd-600-1', '.hd-180-1')),
         '\\.[a-z]{2,3}-40(0[0-9]{3}|1[0-7][0-9]{2})(-[0-9]*)*(?=\\.)', ''))
 WHERE `figure` REGEXP '(^|\\.)[a-z]{2,3}-40(0[0-9]{3}|1[0-7][0-9]{2})(-|\\.|$)';

-- a uniform is laid over the wearer's own head, so a face is simply dropped
UPDATE `rp_uniforms`
   SET `figure` = TRIM(BOTH '.' FROM REGEXP_REPLACE(CONCAT('.', `figure`, '.'),
         '\\.[a-z]{2,3}-40(0[0-9]{3}|1[0-7][0-9]{2})(-[0-9]*)*(?=\\.)', ''))
 WHERE `figure` REGEXP '(^|\\.)[a-z]{2,3}-40(0[0-9]{3}|1[0-7][0-9]{2})(-|\\.|$)';

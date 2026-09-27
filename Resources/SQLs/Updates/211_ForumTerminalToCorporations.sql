-- pixelrp: the Forum Terminal goes on sale in Builders > Corporations.
--
-- It is Habbo's info_terminal_test (sprite 5370), a stand-up terminal that 31
-- imported with the rest of the library and 32 named 'Forum Terminal'. No
-- migration has sold it since 83 replaced 33's catalog - 119 did not shelve it
-- - so it is added, not moved. Corporations is where
-- somebody fitting out an office or a front desk looks, beside the ATM (102)
-- and the Corporation Gate (146). Beta serves its bundle and icon.
--
-- 31 gave the library auto-increment ids, so the furni is found by its
-- classname, never by a number read out of a file. Free, like the rest of the
-- catalog since 90_FreeCatalog.
--
-- Idempotent: nothing is added while Corporations already sells it.

SET @builders := (SELECT `id` FROM `catalog_pages` WHERE `parent_id` = -1 AND `caption` = 'Builders' LIMIT 1);
SET @corps    := (SELECT `id` FROM `catalog_pages` WHERE `parent_id` = @builders AND `caption` = 'Corporations' LIMIT 1);
SET @terminal := (SELECT `id` FROM `furniture` WHERE `item_name` = 'info_terminal_test' ORDER BY `id` LIMIT 1);

INSERT INTO `catalog_items`
    (`page_id`,`item_id`,`catalog_name`,`cost_credits`,`cost_pixels`,`cost_diamonds`,
     `amount`,`limited_sells`,`limited_stack`,`offer_active`,`extradata`,`badge`,`offer_id`)
SELECT @corps, CAST(@terminal AS CHAR), 'Forum Terminal', 0, 0, 0, 1, 0, 0, '1', '', '', -1
  FROM DUAL
 WHERE @corps IS NOT NULL AND @terminal IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM `catalog_items`
                    WHERE `page_id` = @corps AND `item_id` = CAST(@terminal AS CHAR));

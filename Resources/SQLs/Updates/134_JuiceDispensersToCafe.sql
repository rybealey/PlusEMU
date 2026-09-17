-- pixelrp: the juice dispensers move to Builders > Corporations > Cafe.
--
-- They sat on Staff > Rares > Bonus Rares, which is where the default catalog
-- files every bonusrare line regardless of what the piece actually is. A drinks
-- machine belongs on the shelf somebody building a cafe looks at, next to the
-- Jukebox that 83 moved there for the same reason.
--
-- Matched by PREFIX, not by listing the colours. The live furniture table is
-- the authority on which variants exist, not these files - 32 names six
-- (blue, orange, red, purple, black, green) and the page was reported as
-- carrying four, then five, then six as more were spotted. A prefix takes
-- whatever is really there and cannot miss one.
--
-- The escaped underscore matters: in LIKE, `_` is a single-character wildcard,
-- so an unescaped 'bonusrare18_1%' would also match 'bonusrare18X1...'. With it
-- escaped the pattern reaches bonusrare18_1 and its colour variants and nothing
-- from _2, _3 or _4 - which are the mini vases, the lamps and a fourth line
-- that have no business in a cafe.
--
-- A move, not a copy: the existing rows change page, so there are no ids to
-- allocate and nothing to collide with. Anything matching with no catalog row
-- at all gets one, since "move it to Cafe" and "it is not for sale anywhere"
-- should not end in the same silence.
--
-- Only the catalog row moves. The furniture row and its interaction are
-- untouched, so a placed dispenser keeps behaving exactly as it did.
--
-- Idempotent: re-running moves rows that are already there to where they are.

SET @builders := (SELECT `id` FROM `catalog_pages` WHERE `parent_id` = -1 AND `caption` = 'Builders' LIMIT 1);
SET @corps    := (SELECT `id` FROM `catalog_pages` WHERE `parent_id` = @builders AND `caption` = 'Corporations' LIMIT 1);
-- 'Caf_' catches the accented spelling: LIKE counts characters, and 'Café' is
-- four of them. 83 resolves the same page the same way.
SET @cafe     := (SELECT `id` FROM `catalog_pages`
                  WHERE `parent_id` = @corps AND (`caption` = 'Cafe' OR `caption` LIKE 'Caf_') LIMIT 1);

UPDATE `catalog_items` `ci`
    JOIN `furniture` `f` ON `f`.`id` = `ci`.`item_id`
    SET `ci`.`page_id` = @cafe
    WHERE @cafe IS NOT NULL
      AND `f`.`item_name` LIKE 'bonusrare18\_1%';

INSERT INTO `catalog_items`
    (`page_id`,`item_id`,`catalog_name`,`cost_credits`,`cost_pixels`,`cost_diamonds`,
     `amount`,`limited_sells`,`limited_stack`,`offer_active`,`extradata`,`badge`,`offer_id`)
SELECT @cafe, `f`.`id`, COALESCE(NULLIF(`f`.`public_name`, ''), `f`.`item_name`),
       0, 0, 0, 1, 0, 0, '1', '', '', -1
    FROM `furniture` `f`
    WHERE @cafe IS NOT NULL
      AND `f`.`item_name` LIKE 'bonusrare18\_1%'
      AND NOT EXISTS (SELECT 1 FROM `catalog_items` `ci` WHERE `ci`.`item_id` = `f`.`id`);

-- Where the pages resolved to. A NULL cafe id means the page is not called
-- Cafe (or Café) under Corporations any more, and nothing above did anything.
SELECT @builders AS `builders`, @corps AS `corporations`, @cafe AS `cafe`;

-- Every dispenser and the page it now sits on. Each row should read Cafe; one
-- listed with no page has furniture but no catalog row, and one missing
-- entirely is not in the furniture table under that name.
SELECT `f`.`item_name`, `f`.`public_name`, `p`.`caption` AS `page`
    FROM `furniture` `f`
    LEFT JOIN `catalog_items` `ci` ON `ci`.`item_id` = `f`.`id`
    LEFT JOIN `catalog_pages` `p` ON `p`.`id` = `ci`.`page_id`
    WHERE `f`.`item_name` LIKE 'bonusrare18\_1%'
    ORDER BY `f`.`item_name`;

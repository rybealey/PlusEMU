-- pixelrp: the runway dressing rooms move to Builders > Corporations > Clothing.
--
-- Habbo's catalog filed runway_changing with the salon furniture on the Runway
-- page, among sewing machines and seating. Clothing already holds
-- boutique_changing1-3 - the booths 21 turned into working dressing booths -
-- so that is the shelf somebody building a changing room actually looks on.
--
-- Matched by prefix rather than by name. The live furniture table is the
-- authority on which variants of this line exist, not these files: 31 carries
-- an nft_ variant, 32 names a plain one and a rare, and 83's catalog rows name
-- two more. LIKE 'runway_changing%' takes whatever is really there and leaves
-- the nft_ prefixed rare alone, which is a different piece.
--
-- Pages resolved by caption, never by id. Builders and everything under it is
-- data-only - created in the live database and present in no migration - so an
-- id read out of a file here would be a guess dressed up as a fact. 119 had to
-- be rewritten for exactly that mistake.
--
-- A move rather than a copy: the existing rows change page, so there are no
-- ids to allocate and nothing to collide with. Anything matching that has no
-- catalog row at all gets one, since "move it to Clothing" and "it is not for
-- sale anywhere" should not end in the same silence.
--
-- Idempotent: re-running moves rows that are already there to where they are.

SET @builders := (SELECT `id` FROM `catalog_pages` WHERE `parent_id` = -1 AND `caption` = 'Builders' LIMIT 1);
SET @corps    := (SELECT `id` FROM `catalog_pages` WHERE `parent_id` = @builders AND `caption` = 'Corporations' LIMIT 1);
SET @clothing := (SELECT `id` FROM `catalog_pages` WHERE `parent_id` = @corps AND `caption` = 'Clothing' LIMIT 1);

UPDATE `catalog_items` `ci`
    JOIN `furniture` `f` ON `f`.`id` = `ci`.`item_id`
    SET `ci`.`page_id` = @clothing
    WHERE @clothing IS NOT NULL
      AND `f`.`item_name` LIKE 'runway\_changing%';

INSERT INTO `catalog_items`
    (`page_id`,`item_id`,`catalog_name`,`cost_credits`,`cost_pixels`,`cost_diamonds`,
     `amount`,`limited_sells`,`limited_stack`,`offer_active`,`extradata`,`badge`,`offer_id`)
SELECT @clothing, `f`.`id`, `f`.`item_name`, 0, 0, 0, 1, 0, 0, '1', '', '', -1
    FROM `furniture` `f`
    WHERE @clothing IS NOT NULL
      AND `f`.`item_name` LIKE 'runway\_changing%'
      AND NOT EXISTS (SELECT 1 FROM `catalog_items` `ci` WHERE `ci`.`item_id` = `f`.`id`);

-- Where the pages resolved to. A NULL clothing id means the page is not called
-- 'Clothing' under 'Corporations' any more, and nothing above did anything.
SELECT @builders AS `builders`, @corps AS `corporations`, @clothing AS `clothing`;

-- Every matching piece and the page it now sits on. Each row should read
-- Clothing; a piece listed with no page has furniture but no catalog row, and
-- one missing entirely is not in the furniture table under that name.
SELECT `f`.`item_name`, `f`.`public_name`, `p`.`caption` AS `page`
    FROM `furniture` `f`
    LEFT JOIN `catalog_items` `ci` ON `ci`.`item_id` = `f`.`id`
    LEFT JOIN `catalog_pages` `p` ON `p`.`id` = `ci`.`page_id`
    WHERE `f`.`item_name` LIKE 'runway\_changing%'
    ORDER BY `f`.`item_name`;

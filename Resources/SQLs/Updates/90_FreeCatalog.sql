-- PixelRP: nothing in the catalog costs anything.
--
-- Credits, duckets and diamonds all go to zero, hotel-wide - Builders
-- included. The tab-structure rule ("do not touch Builders") is about what
-- lives where; a price is not structure, which is the same reasoning
-- 83_CatalogRestoreDefault used to fold duckets into credits everywhere.
--
-- `catalog_items` is the only price surface in the schema: catalog_deals holds
-- item lists with no cost, and the marketplace is player-priced rather than
-- catalogued.
UPDATE `catalog_items`
    SET `cost_credits` = 0, `cost_pixels` = 0, `cost_diamonds` = 0
    WHERE `cost_credits` > 0 OR `cost_pixels` > 0 OR `cost_diamonds` > 0;

-- The column shipped defaulting to 3, so a future INSERT that leaves the price
-- out would quietly reintroduce a charge. Zero is the house default now, which
-- makes this a policy rather than a one-off sweep.
ALTER TABLE `catalog_items`
    ALTER COLUMN `cost_credits` SET DEFAULT 0;

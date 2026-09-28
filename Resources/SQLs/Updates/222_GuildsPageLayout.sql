-- pixelrp: the Guilds catalog page gets its group picker back.
--
-- 119 recreated Guilds on the plain default_3x3 layout. Only guild_custom_furni
-- shows the group selector, and the selector is what sends the group a piece
-- is bought for - so on default_3x3 the client sent an empty string, and the
-- emulator's Convert.ToInt32 on it threw and disconnected the buyer. The
-- emulator no longer throws (PurchaseFromCatalogEvent), but a group furni page
-- with no way to pick a group sells nothing but neutral furni, which is not
-- what Guilds is for.
--
-- Turfs, the other group furni page (83), stays default_3x3 on purpose: turf
-- furni is bought neutral (group 0) and takes the owning gang's colours in the
-- room it is placed in.
--
-- Matched on the caption AND on the page actually holding group furni, never
-- the caption alone: captions are not unique in this catalog, and a hub and
-- its first child often share one.
--
-- Idempotent.
UPDATE `catalog_pages` `p`
SET `p`.`page_layout` = 'guild_custom_furni'
WHERE `p`.`caption` = 'Guilds'
  AND `p`.`page_layout` = 'default_3x3'
  AND EXISTS (SELECT 1 FROM `catalog_items` `ci`
              JOIN `furniture` `f` ON `f`.`id` = `ci`.`item_id`
              WHERE `ci`.`page_id` = `p`.`id`
                AND `f`.`interaction_type` IN ('gld_item', 'gld_gate', 'guild_forum'));

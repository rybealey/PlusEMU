-- PixelRP: clothing leaves the shop entirely. Clothing is granted via the
-- avatar editor (catalog_clothing gates sellable sets to staff rank >= 4 and
-- strips them from everyone else) - the catalog's clothing boxes were never
-- redeemable anyway (behaviour_data = 0). Removes every catalog row whose
-- furniture is a clothing box: the 134 purchasable_clothing items plus every
-- clothing_* classname that entered the catalog with the full furni library.
DELETE ci FROM `catalog_items` ci
JOIN `furniture` f ON f.`id` = CAST(ci.`item_id` AS UNSIGNED)
WHERE (f.`item_name` LIKE 'clothing\_%' OR f.`interaction_type` = 'purchasable_clothing');

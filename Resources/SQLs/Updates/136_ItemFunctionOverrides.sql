-- pixelrp: behaviour set on ONE placed furni instead of every copy of it.
--
-- The Function tool edits a DEFINITION, hotel-wide: that is its whole design,
-- and it is right for "this piece of furni is a chair". It is wrong for "this
-- one mat, in this one shop, opens the clothing store" - which until now meant
-- either converting every copy in the hotel or importing a duplicate furni.
--
-- Sparse on purpose. A row exists only for a field somebody actually overrode,
-- so the cost of the feature is zero for the hundreds of thousands of items
-- that use their definition as-is, and an override survives a later edit to the
-- definition rather than being silently overwritten by one.
--
-- Keyed by item id, so an override follows the furni into inventory and back
-- out again. Picking something up and putting it down should not quietly undo
-- the thing you configured.
--
-- WHAT CAN BE OVERRIDDEN, and why it is not everything:
--
--   yes  interaction_type, modes, effect_id, behaviour_data, vending_ids
--   no   public_name, walkable, seat, stackable, height, height_marker,
--        adjustable_heights, walk_mask
--
-- The client keeps its own copy of the second group in FurnitureData, which is
-- keyed by furni CLASS and not by item - PatchFurnitureData writes exactly
-- _localizedName, _canStandOn, _canSitOn, _canLayOn. Overriding one of those
-- per item would desync: nitro refuses to compute a walk target for a furni
-- whose canStandOn/canSitOn/canLayOn are all false, so the server would path to
-- a tile no player could click.
--
-- The one seam in the first group: _canLayOn is DERIVED client-side from the
-- interaction type ('bed' or 'tent_small'), so scoping an interaction type to
-- or from a laying type is refused for the same reason. RpFurniFunctionEvent
-- enforces that; this table just stores what it allowed.
--
-- set_by is kept for the same reason rp_furni_function_log exists: a change
-- nobody can attribute is how a build tool turns into an argument.

CREATE TABLE IF NOT EXISTS `rp_item_function` (
  `item_id` int(10) unsigned NOT NULL,
  `field` varchar(32) NOT NULL,
  `value` varchar(255) NOT NULL DEFAULT '',
  `set_by` int(11) NOT NULL DEFAULT 0,
  `created_at` int(11) NOT NULL DEFAULT 0,
  -- One value per field per item, so re-setting one replaces it.
  PRIMARY KEY (`item_id`, `field`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

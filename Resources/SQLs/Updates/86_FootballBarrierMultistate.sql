-- The Football Barrier double-clicks between its two states again.
--
-- fball_cote's bundle is logicType furniture_multistate with two animations
-- (0 and 1), so the client has always been willing to draw both states. The
-- furniture row is the half that was wrong: interaction_type 'default' routes
-- to InteractorGenericSwitch, whose first act is
--
--     var modes = item.Definition.Modes - 1;
--     if (session == null || !hasRights || modes <= 0) return;
--
-- and with interaction_modes_count = 1 that is modes = 0, so the double click
-- returned before touching the item. Two states means a count of 2.
--
-- Keyed on item_name, not the id, so it does not depend on this environment's
-- furniture ids. Idempotent, and it will not lower a count somebody has
-- already raised.
UPDATE `furniture`
   SET `interaction_modes_count` = 2
 WHERE `item_name` = 'fball_cote'
   AND `interaction_modes_count` < 2;

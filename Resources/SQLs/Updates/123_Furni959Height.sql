-- pixelrp: put back the height the Tools panel flattened.
--
-- bsstonino_furni959 was imported by 122 with stack_height 1.0. The furni log
-- shows it going 1 -> 0 as its own save, seconds after an unrelated Movement
-- and Collision change, which is what made the whole panel look like it was
-- not persisting. It persisted exactly what the client gave it.
--
-- The client's fault, since fixed: the Tools panel's number fields read
-- `parseFloat(value) || 0` straight into the draft, and `parseFloat('')` is
-- NaN while `NaN || 0` is 0. One backspace before retyping a height wrote a
-- zero, and Apply saves every field rather than the one you touched. A field
-- being edited is no longer treated as a value.
--
-- Guarded on the damaged value, so this is inert anywhere the zero never
-- happened - including prod, which has not seen the broken panel.

UPDATE `furniture`
    SET `stack_height` = 1.0
    WHERE `item_name` = 'bsstonino_furni959'
      AND `stack_height` = 0;

-- Should read 1.0.
SELECT `item_name`, `stack_height`, `is_walkable`, `can_sit`, `can_stack`
    FROM `furniture` WHERE `item_name` = 'bsstonino_furni959';

-- Anything else the same bug may have flattened while the panel was open.
-- REPORTED, NOT CHANGED: a height of zero is a legitimate value for a floor
-- tile, and 122 gives three pieces exactly that, so guessing which zeros are
-- damage is not this migration's call to make. Three names below are expected.
SELECT `item_name`, `stack_height` FROM `furniture`
    WHERE `item_name` LIKE 'bsstonino\_furni%' AND `stack_height` = 0
    ORDER BY `item_name`;

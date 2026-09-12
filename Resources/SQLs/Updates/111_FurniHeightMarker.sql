-- PixelRP: the floating blue ring over a tile can be switched off per furni.
--
-- What the ring is: nitro's TILE CURSOR has a second state. Hovering a tile
-- normally draws the white diamond on the floor. But RoomObjectEventHandler's
-- handleMouseOverTile checks the top object on that tile for the model value
-- `furniture_is_variable_height`, and when it is set it sends the tile's
-- STACK HEIGHT with the cursor instead of zero. TileCursorLogic then switches
-- the cursor to state 6 whenever that height is over 0.8, and
-- TileCursorVisualization lifts the cursor's second layer by height * 32 -
-- which is the ring, floating at the height a dropped item would land on.
--
-- Nothing sets that flag from the database. FurnitureMultiHeightLogic sets it
-- on initialize, and a furni only gets that logic because its own .nitro
-- bundle names `furniture_multiheight`. So the marker is decided by the ASSET,
-- and until now the only way to stop it was to rebuild the bundle.
--
-- 1 is show, and the default, so every existing furni behaves exactly as it
-- does today. Turning it off is a display choice only: it hides the height
-- READOUT, not the height - stacking still works and still lands where it
-- always did.
--
-- This is a client-side flag with no server behaviour behind it, which is why
-- it is not in `interaction_type`: it says how a furni is drawn, not what it
-- does.

ALTER TABLE `furniture`
    ADD COLUMN `height_marker` TINYINT(1) NOT NULL DEFAULT 1 AFTER `interaction_type`;

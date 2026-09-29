-- pixelrp: promoted rooms are gone.
--
-- Stock Habbo let an owner buy a room "event" - a title and description shown
-- to everyone in the room and listed on the Navigator's Events tab
-- (roomads_view). PixelRP drops the feature: the tab is no longer sent
-- (NavigatorManager), the server no longer has any promotion code (the
-- purchase, edit and listing handlers and the room's promotion data are
-- removed), and the client no longer shows the room banner or the shop page.
--
-- This one-time step ends every running promotion, switches off the shop page
-- that sold them and the Navigator list behind the old tab. Rows are disabled,
-- not deleted, so the pages stay recoverable.

DELETE FROM `room_promotions`;

UPDATE `catalog_pages` SET `enabled` = '0', `visible` = '0' WHERE `page_layout` = 'roomads';

UPDATE `navigator_categories` SET `enabled` = '0' WHERE `category` = 'roomads_view';

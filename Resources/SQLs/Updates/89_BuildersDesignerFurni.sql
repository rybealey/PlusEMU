-- PixelRP: Builders gets Designer Furni (Kasja moves inside it) + Habba Creators.
--
-- Regroups the custom-furni end of the Builders tab ahead of the habba.io
-- customs import. Kasja stops being a top-level Builders category and becomes
-- the first designer under a new Designer Furni parent; Habba Creators is
-- created alongside it as the landing page for the Creators set.
--
-- Both new pages take the slot Kasja used to hold, so the tab still reads as
-- three groups below the divider that 84 planted (Cartier | Club | customs):
--
--   Builders
--     Navigation                     order 3
--     -                              order 1000  (divider, 59)
--     Cartier                        order 1001  (59)
--     Club                           order 1002  (83)
--     -                              order 1003  (divider, 84)
--     Designer Furni                 order 1004  <- was Kasja's slot
--       Kasja                          order 1    <- reparented, pack pages ride along
--     Habba Creators                 order 1005  <- dark, see below
--
-- Habba Creators ships `visible` = 0 because it has no furni yet. Visible is
-- not a server-side tree filter (CatalogIndexComposer only filters on
-- CanSee, i.e. rank/VIP) - it is written into the index as a per-node flag the
-- client keys on to decide whether to draw the node. So a visible = 0 page is
-- still sent and still fully wired, it just is not rendered. `enabled` stays 1
-- so the furni import only has to flip visible to turn it on as a real page,
-- rather than re-deriving the page row.
--
-- Kasja's own child pages (940003-940011) and every catalog_items / furniture
-- row from 84 are untouched: they hang off page 940002, which only changes
-- parent here.
--
-- Ids sit at 941000+, clear of the 940000-940999 block 84 clears on insert, so
-- the two migrations cannot interact on a from-scratch replay.
--
-- Idempotent: fixed ids cleared before insert, the slot is derived from Club
-- (which this file never moves) rather than from Kasja's own current order_num,
-- and the reparent is a no-op if Kasja is already in place.

SET @builders := COALESCE(
    (SELECT `id` FROM `catalog_pages` WHERE `parent_id` = -1 AND `caption` = 'Builders' LIMIT 1),
    912362);

-- Club never moves, so club + 2 reproduces Kasja's slot (84 put the divider at
-- club + 1 and Kasja at divider + 1) and stays correct on a re-run, which
-- reading Kasja's live order_num would not.
SET @club_order := (SELECT `order_num` FROM `catalog_pages`
                    WHERE `parent_id` = @builders AND `caption` = 'Club' LIMIT 1);
SET @slot := COALESCE(@club_order, 1002) + 2;

-- By id first: after this file has run once Kasja is no longer a child of
-- @builders, so a parent-scoped lookup would come back empty on a re-run.
SET @kasja := COALESCE(
    (SELECT `id` FROM `catalog_pages` WHERE `id` = 940002 AND `caption` = 'Kasja' LIMIT 1),
    (SELECT `id` FROM `catalog_pages` WHERE `caption` = 'Kasja' ORDER BY `id` LIMIT 1));

DELETE FROM `catalog_pages` WHERE `id` IN (941000, 941001);

INSERT INTO `catalog_pages`
    (`id`,`parent_id`,`caption`,`icon_image`,`min_rank`,`min_vip`,`order_num`,`page_link`,
     `page_layout`,`page_strings_1`,`page_strings_2`,`visible`,`enabled`)
VALUES
    (941000, @builders, 'Designer Furni', 193, 2, 0, @slot, '', 'default_3x3', '', '', b'1', b'1'),
    (941001, @builders, 'Habba Creators', 193, 2, 0, @slot + 1, '', 'default_3x3', '', '', b'0', b'1');

-- No-op when @kasja is NULL (page already gone), so this cannot orphan anything.
UPDATE `catalog_pages` SET `parent_id` = 941000, `order_num` = 1 WHERE `id` = @kasja;

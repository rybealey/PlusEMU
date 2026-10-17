-- pixelrp: withdraw the Habzone clothing (264, 265, 266) for now. The import's
-- figure sets are gone from emulator/Config/figuredata.xml and its bundles and
-- gamedata from beta (nitro/overrides/gamedata-remove/habzone-clothes.json), so
-- its shelf rows go too. Every one of them starts with a set in the import's
-- own block, 400000-401792, which nothing else uses.
--
-- user_clothing is left alone: what a player bought stays recorded, and a
-- later re-import keeps the same set ids (docker/nitro/habzone-clothes/ids.csv),
-- so it would come back owned.
DELETE FROM `catalog_clothing`
 WHERE CAST(SUBSTRING_INDEX(`clothing_parts`, ',', 1) AS UNSIGNED) BETWEEN 400000 AND 401792;

-- Corporation stock: a plain quantity the corporation holds. A placeholder
-- value for now (always 0) so the Corporations window can surface it; once
-- farming lands this becomes the supply a business consumes to serve
-- customers - ingredients for a juice bar, for example.

ALTER TABLE `rp_corporations`
  ADD COLUMN `stock` int NOT NULL DEFAULT 0;

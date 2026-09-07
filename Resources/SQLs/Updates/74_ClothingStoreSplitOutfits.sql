-- pixelrp: the Clothing Store sells pieces, not outfits. Eighteen of the
-- imported catalog_clothing rows are whole costumes spanning several figure
-- slots at once (Santa Claus Suit = top + trousers + hat + beard), which
-- undercuts the individual pieces sitting next to them on the same shelf.
--
-- Pull the costumes off the shelf with price = 0 (73_ClothingStore.sql:
-- "0 hides the piece from the store") rather than DELETE: the rows still gate
-- their part ids for ProcessFigure, still resolve for any legacy
-- purchasable_clothing furni sitting in an inventory, and can be put back with
-- one UPDATE. Nobody loses anything either - user_clothing records ownership
-- per part id, so a player who bought a costume already owns its pieces.
UPDATE `catalog_clothing` SET `price` = 0 WHERE `clothing_name` IN (
  'clothing_meowfit',        -- ha + ch + lg + sh
  'clothing_mermaidoutfit',  -- ch + lg
  'clothing_mockymouse',     -- wa + mc + lg
  'clothing_kimono1',        -- wa + ch + hr + lg
  'clothing_kimono2',        -- ch + hr + lg
  'clothing_cygirl',         -- cc + sh + he + hr + lg
  'clothing_cystraphood',    -- cc + ha + sh + lg
  'clothing_candyboy',       -- cc + lg + ha
  'clothing_candygirl',      -- ha + hr + cc + lg
  'clothing_camooutfit',     -- ha + ch + lg + sh
  'clothing_kevlaroutfit',   -- cc + ha
  'clothing_parade',         -- cc + lg + ha
  'clothing_dino',           -- ha + ch + lg + sh
  'clothing_caveman',        -- ch + hr
  'clothing_demonoutfit',    -- ha + ch + ca
  'clothing_knightoutfit',   -- cc + lg
  'clothing_cladyoutfit',    -- ch + sh + hr + he + ca
  'clothing_santaoutfit'     -- ch + lg + ha + fa
);

-- Two single-piece rows point at part ids that do not exist in figuredata
-- (8462 and 8493 look like furni ids that got pasted into clothing_parts), so
-- they have never been wearable - the real trousers and hair were only
-- obtainable inside the costumes above. Repoint them at the real parts and
-- carry over anyone who already "owns" the dead id.
UPDATE `user_clothing` SET `part_id` = '3460' WHERE `part_id` = '8462';
UPDATE `user_clothing` SET `part_id` = '3468' WHERE `part_id` = '8493';
UPDATE `catalog_clothing` SET `clothing_parts` = '3460' WHERE `clothing_name` = 'clothing_santapants';
UPDATE `catalog_clothing` SET `clothing_parts` = '3468' WHERE `clothing_name` = 'clothing_cladyhair';

-- The remaining 24 parts of the delisted costumes had no individual listing at
-- all, so they get one each. Where a garment ships as a male and a female set
-- both ids go on one row (that is one piece to a player), matching how
-- clothing_camotank and clothing_knighttop already list theirs. Same 50 credit
-- opening price as everything else; the client tidies clothing_name into the
-- shelf label, so display_name stays NULL.
INSERT INTO `catalog_clothing` (`clothing_name`, `clothing_parts`, `price`) VALUES
-- Meow-suit
('clothing_meow_ears', '3331', 50),
('clothing_meow_top', '3334,3335', 50),
('clothing_meow_leggings', '3337', 50),
('clothing_meow_paws', '3338', 50),
-- Mermaid Outfit
('clothing_mermaid_top', '3332', 50),
('clothing_mermaid_tail', '3333', 50),
-- Mocky Maus
('clothing_mocky_maus_belt', '3359', 50),
('clothing_mocky_maus_gloves', '3360', 50),
('clothing_mocky_maus_shorts', '3361', 50),
-- Floral Kimono
('clothing_floral_kimono_obi', '3366', 50),
('clothing_floral_kimono_top', '3367,3368', 50),
('clothing_floral_kimono_hair', '3369', 50),
('clothing_floral_kimono_skirt', '3364', 50),
-- Blue Kimono
('clothing_blue_kimono_top', '3371,3372', 50),
('clothing_blue_kimono_hair', '3370', 50),
('clothing_blue_kimono_skirt', '3365', 50),
-- Military Parade Uniform (the jacket, hat and one trouser already sell alone)
('clothing_parade_trousers', '3408', 50),
-- Dino onesie (the hat already sells alone as clothing_dinohat)
('clothing_dino_onesie_top', '3432,3433', 50),
('clothing_dino_onesie_legs', '3434', 50),
('clothing_dino_onesie_feet', '3435', 50);

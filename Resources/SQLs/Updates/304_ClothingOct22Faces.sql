-- pixelrp: faces are not sold in the Clothing Store. 303 priced the October
-- 22 import's one face (face_U_bratzkobyface, set 500111) at 50 like the rest;
-- it goes to 0, owned-only, like every other face (240, 257, 262). The store
-- shows only priced rows, and its Head tab no longer lists faces.
UPDATE `catalog_clothing` SET `price` = 0 WHERE `clothing_parts` = '500111';

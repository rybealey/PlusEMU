-- pixelrp: weapon and body armour durability (HabboHotel/Users/RpDurability).
--
-- A bat, knife or axe wears by the damage its hits deal; body armour by the
-- damage its wearer takes. Each is good for 1200 before it breaks (Twist,
-- 2026-10-25). Weapons and armour never stack - one to a row - so the wear is
-- kept on the row and goes wherever the row goes: between slots, into the
-- deposit box and back, and across a trade. Every other item keeps 0.

ALTER TABLE `user_rp_inventory`
    ADD COLUMN `wear` INT NOT NULL DEFAULT 0 AFTER `count`;

ALTER TABLE `user_rp_deposit_box`
    ADD COLUMN `wear` INT NOT NULL DEFAULT 0 AFTER `count`;

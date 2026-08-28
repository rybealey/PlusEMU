-- PixelRP: gate the 22 newly imported official clothing sets (2026 wave -
-- academia / halloween / pets). One catalog_clothing row per set marks it
-- sellable-gated: rank >= 4 staff see and wear them via FullWardrobeUtility;
-- everyone else has them stripped on look save (ProcessFigure). No catalog
-- furni exists for these - clothing is no longer sold in the shop.
INSERT INTO `catalog_clothing` (`clothing_name`, `clothing_parts`) VALUES
('acc_head_U_ducketleafcrown', '6536'),
('hair_U_academiadollhairdo', '6551'),
('misc_U_ruffledcuffs', '6552'),
('shoes_U_loosesocks', '6554'),
('pet_U_raven', '6555'),
('face_U_academiagothicmakeup', '6556'),
('face_U_academiagothicmakeup2', '6557'),
('pet_U_hauntedpuppet', '6558'),
('pet_U_persiancat', '6559'),
('acc_chest_U_vintagebag', '6560'),
('hat_U_batonesie', '6563'),
('shirt_U_batonesie', '6564'),
('pet_U_batfriend', '6565'),
('hair_U_wizardcurls', '6566'),
('pet_U_tinyowl', '6567'),
('jacket_M_studentcardigan', '6568'),
('jacket_F_studentcardigan', '6569'),
('jacket_M_wizardgarb', '6572'),
('jacket_F_wizardgarb', '6573'),
('misc_U_wand', '6574'),
('pet_U_possessedpumpkin', '6577'),
('acc_head_U_skeletalheadband', '6580');

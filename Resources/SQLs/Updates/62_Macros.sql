-- pixelrp macros: per-player key/mouse bindings, stored server-side so a
-- player's macros follow them to any browser or machine (the master switch and
-- the bindings used to live in localStorage, which did not).
--
-- One row per user holding the whole macro document as JSON: the enabled flag,
-- the active preset name, and every preset with its ordered bindings. A blob
-- rather than a row per binding because nothing server-side ever reads an
-- individual macro - the emulator is purely a locker here, and the client
-- always saves and loads the document whole, so ordering and atomic replacement
-- come for free. RpSaveMacrosEvent validates and re-serialises before this is
-- written, so the column never holds client-supplied text verbatim.
CREATE TABLE IF NOT EXISTS `user_macros` (
  `user_id` INT(11) NOT NULL,
  `data` TEXT NOT NULL,
  PRIMARY KEY (`user_id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

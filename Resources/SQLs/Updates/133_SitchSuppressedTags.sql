-- pixelrp: tags staff have taken out of circulation.
--
-- A suppressed tag returns no posts on a search and never appears in trending.
-- It does NOT delete anything: the posts stand, the tag rows stand, and
-- lifting the suppression puts everything back exactly as it was. That is the
-- difference between "the city stops amplifying this" and "this never
-- happened", and only the first is staff's to decide with one button.
--
-- The tag is the primary key, so suppressing twice is the same as suppressing
-- once and a re-run costs nothing.
--
-- Stored lowercased, matching rp_sitch_tags, so #Muse and #muse are one tag
-- here as well - suppressing one spelling has to suppress them all or the
-- button does nothing.
--
-- suppressed_by is kept because a quiet moderation action nobody can attribute
-- is how a moderation tool turns into an argument.

CREATE TABLE IF NOT EXISTS `rp_sitch_suppressed_tags` (
  `tag` varchar(64) NOT NULL,
  `suppressed_by` int(11) NOT NULL DEFAULT 0,
  `created_at` int(11) NOT NULL DEFAULT 0,
  PRIMARY KEY (`tag`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

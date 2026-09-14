-- pixelrp: hashtags on Sitch.
--
-- A tag is extracted when a post is written and stored here, rather than found
-- later with LIKE '%#tag%'. Three reasons that matters:
--
--   * LIKE with a leading wildcard cannot use an index, so every tag search
--     would read every post in the hotel.
--   * It is wrong as often as it is right - '%#art%' also matches "#article"
--     and an email address, and misses nothing usefully.
--   * A tag table is what makes "what is the city talking about" answerable
--     later without touching the posts at all.
--
-- Tags are stored lowercased, so #Muse and #muse are one tag.
--
-- Mentions deliberately get NO table. What a mention has to do is tell the
-- person they were mentioned, and rp_sitch_activity already does that; finding
-- posts that mention a name is the body search, which is bounded by the same
-- page size as everything else.

CREATE TABLE IF NOT EXISTS `rp_sitch_tags` (
  `post_id` int(11) NOT NULL,
  `tag` varchar(64) NOT NULL,
  `created_at` int(11) NOT NULL DEFAULT 0,
  -- A post uses a tag at most once, however many times it is typed.
  PRIMARY KEY (`post_id`, `tag`),
  -- The one query this table exists for: every post carrying a tag, newest
  -- first.
  KEY `tag_created` (`tag`, `created_at`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

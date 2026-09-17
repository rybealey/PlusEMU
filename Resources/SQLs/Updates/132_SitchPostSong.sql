-- pixelrp: a song attached to a Sitch post.
--
-- The same shape the profile's favorite song uses (127_Sitch), and for the
-- same reasons: the 11-character YouTube id is the identity, and the title and
-- author are what oEmbed returned when the post was written, kept so a
-- timeline renders without a network call per post.
--
-- Still no duration column. oEmbed does not return one, and the only reason
-- the jukebox knows a duration is that a client reports it back after playing;
-- a post is not a player, so it would never learn one.
--
-- Nullable is not used - '' is "no song", matching rp_sitch_profiles, so
-- reading a post never has to distinguish empty from absent.
--
-- No index. Nothing searches by song; it is read with the post it belongs to.

ALTER TABLE `rp_sitch_posts`
    ADD COLUMN `song_video_id` varchar(16) NOT NULL DEFAULT '' AFTER `photo_id`,
    ADD COLUMN `song_title` varchar(160) NOT NULL DEFAULT '' AFTER `song_video_id`,
    ADD COLUMN `song_author` varchar(80) NOT NULL DEFAULT '' AFTER `song_title`;

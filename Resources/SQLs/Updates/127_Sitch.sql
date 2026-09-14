-- pixelrp: Sitch, the city's own feed.
--
-- Short posts, the replies they start, likes, reposts, a one-directional
-- follow graph, and a profile carrying one favorite song.
--
-- Why a follow graph of its own rather than `messenger_friendships`: a
-- friendship there is MUTUAL, consent-based (request then accept), stored as
-- two rows, and capped by the messenger. A follow is one-directional,
-- uncapped, and needs no consent. Reading one as the other would mean either
-- breaking the messenger's rules or lying about what a follow is.
--
-- Why an activity table when NotificationUtility exists: that is push-only and
-- deliberately keeps no state ("There is no state here and no table"), so it
-- can tell somebody about a like as it happens but cannot answer "what
-- happened while I was away". The Activity tab is exactly that question.
--
-- House style throughout: `rp_` prefix, snake_case, int(11) unix timestamps,
-- tinyint(1) booleans, InnoDB/utf8mb4, no foreign keys, composite primary keys
-- on the join tables.

CREATE TABLE IF NOT EXISTS `rp_sitch_posts` (
  `id` int(11) NOT NULL AUTO_INCREMENT,
  `user_id` int(11) NOT NULL,
  -- 0 for a top-level post; otherwise the post this one replies to. Replies
  -- are posts, so everything below (likes, reposts, deletion) applies to them
  -- without a second set of tables.
  `parent_id` int(11) NOT NULL DEFAULT 0,
  `body` varchar(280) NOT NULL DEFAULT '',
  -- camera_web.id, or 0 for no photo. The row there is the source of truth for
  -- the url and the room name; only ownership is checked at post time.
  `photo_id` int(11) NOT NULL DEFAULT 0,
  `created_at` int(11) NOT NULL DEFAULT 0,
  -- Soft delete, so a removed post stays auditable. deleted_by is the staff
  -- member when staff removed it, or the author when they removed their own.
  `deleted_at` int(11) NOT NULL DEFAULT 0,
  `deleted_by` int(11) NOT NULL DEFAULT 0,
  PRIMARY KEY (`id`),
  KEY `user_id` (`user_id`),
  -- The feed reads live top-level posts newest first; the thread view reads
  -- one parent's replies oldest first. Both are covered here.
  KEY `parent_created` (`parent_id`, `deleted_at`, `created_at`),
  KEY `created_at` (`created_at`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

CREATE TABLE IF NOT EXISTS `rp_sitch_likes` (
  `post_id` int(11) NOT NULL,
  `user_id` int(11) NOT NULL,
  `created_at` int(11) NOT NULL DEFAULT 0,
  PRIMARY KEY (`post_id`, `user_id`),
  KEY `user_id` (`user_id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

CREATE TABLE IF NOT EXISTS `rp_sitch_reposts` (
  `post_id` int(11) NOT NULL,
  `user_id` int(11) NOT NULL,
  `created_at` int(11) NOT NULL DEFAULT 0,
  PRIMARY KEY (`post_id`, `user_id`),
  KEY `user_id` (`user_id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

CREATE TABLE IF NOT EXISTS `rp_sitch_follows` (
  `follower_id` int(11) NOT NULL,
  `followee_id` int(11) NOT NULL,
  `created_at` int(11) NOT NULL DEFAULT 0,
  PRIMARY KEY (`follower_id`, `followee_id`),
  -- "who follows me", which the profile's follower count and the activity
  -- feed both ask.
  KEY `followee_id` (`followee_id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

CREATE TABLE IF NOT EXISTS `rp_sitch_profiles` (
  `user_id` int(11) NOT NULL,
  `bio` varchar(160) NOT NULL DEFAULT '',
  -- The favorite song. The 11-character YouTube id is the identity; title and
  -- author are what oEmbed returned when it was saved, kept so a profile
  -- renders without a network call every time somebody opens it. There is
  -- deliberately no duration column - oEmbed does not return one, and the only
  -- reason the jukebox knows a duration is that a client reports it back after
  -- playing, which a profile card never does.
  `favorite_video_id` varchar(16) NOT NULL DEFAULT '',
  `favorite_title` varchar(160) NOT NULL DEFAULT '',
  `favorite_author` varchar(80) NOT NULL DEFAULT '',
  `updated_at` int(11) NOT NULL DEFAULT 0,
  PRIMARY KEY (`user_id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

CREATE TABLE IF NOT EXISTS `rp_sitch_activity` (
  `id` int(11) NOT NULL AUTO_INCREMENT,
  -- Who this happened TO. The actor is the other half.
  `user_id` int(11) NOT NULL,
  `actor_id` int(11) NOT NULL,
  -- like | reply | repost | follow
  `kind` varchar(16) NOT NULL DEFAULT '',
  -- The post it happened to, or 0 for a follow.
  `post_id` int(11) NOT NULL DEFAULT 0,
  `created_at` int(11) NOT NULL DEFAULT 0,
  `seen` tinyint(1) NOT NULL DEFAULT 0,
  PRIMARY KEY (`id`),
  KEY `user_created` (`user_id`, `created_at`),
  KEY `user_seen` (`user_id`, `seen`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

-- Staff removal of somebody else's post. Registered in permissions_commands
-- like rp_furni_function (103_FurniFunction) even though it gates a UI action
-- rather than a chat command - that is where this hotel keeps UI permissions.
-- Inert until the write path reads it.
INSERT INTO `permissions_commands` (`command`, `group_id`, `subscription_id`)
VALUES ('rp_sitch_moderate', 7, 0)
ON DUPLICATE KEY UPDATE `group_id` = 7;

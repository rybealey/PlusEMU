-- pixelrp: the phone's Notes app. Notes belong to an owner and may be shared
-- with friends (rp_note_shares). Folders are personal: the owner files a note
-- via rp_notes.folder_id, each collaborator via rp_note_shares.folder_id
-- (NULL = "Shared with you"). Body is plain text, one block per line:
-- "- " bullets and "[ ] " / "[x] " checklist items.
CREATE TABLE IF NOT EXISTS `rp_note_folders` (
  `id` int(11) NOT NULL AUTO_INCREMENT,
  `user_id` int(11) NOT NULL,
  `name` varchar(32) NOT NULL,
  `sort_order` int(11) NOT NULL DEFAULT 0,
  `created_at` int(11) NOT NULL,
  PRIMARY KEY (`id`),
  KEY `owner` (`user_id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

CREATE TABLE IF NOT EXISTS `rp_notes` (
  `id` int(11) NOT NULL AUTO_INCREMENT,
  `owner_id` int(11) NOT NULL,
  `folder_id` int(11) DEFAULT NULL,
  `title` varchar(80) NOT NULL DEFAULT '',
  `body` text NOT NULL,
  `pinned` tinyint(1) NOT NULL DEFAULT 0,
  `version` int(11) NOT NULL DEFAULT 1,
  `created_at` int(11) NOT NULL,
  `updated_at` int(11) NOT NULL,
  `updated_by` int(11) NOT NULL,
  PRIMARY KEY (`id`),
  KEY `owner` (`owner_id`),
  KEY `folder` (`folder_id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

CREATE TABLE IF NOT EXISTS `rp_note_shares` (
  `note_id` int(11) NOT NULL,
  `user_id` int(11) NOT NULL,
  `folder_id` int(11) DEFAULT NULL,
  `added_at` int(11) NOT NULL,
  PRIMARY KEY (`note_id`, `user_id`),
  KEY `user` (`user_id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

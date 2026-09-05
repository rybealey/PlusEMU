-- pixelrp: the phone's News app. Staff-written stories; image is a file name
-- from the CMS article image library (cms/public/assets/images/articles).
CREATE TABLE IF NOT EXISTS `rp_news_posts` (
  `id` int(11) NOT NULL AUTO_INCREMENT,
  `author_id` int(11) NOT NULL,
  `category` varchar(24) NOT NULL DEFAULT 'Hotel',
  `title` varchar(120) NOT NULL,
  `body` text NOT NULL,
  `image` varchar(160) NOT NULL DEFAULT '',
  `pinned` tinyint(1) NOT NULL DEFAULT 0,
  `created_at` int(11) NOT NULL,
  `updated_at` int(11) NOT NULL,
  PRIMARY KEY (`id`),
  KEY `recent` (`pinned`, `created_at`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

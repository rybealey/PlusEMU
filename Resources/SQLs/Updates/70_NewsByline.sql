-- pixelrp: News stories can be published under the newsroom byline (Trina)
-- instead of the writer's own name. author_id stays the real writer.
ALTER TABLE `rp_news_posts` ADD COLUMN `anonymous` tinyint(1) NOT NULL DEFAULT 0 AFTER `pinned`;
-- the Hotel category is retired
UPDATE `rp_news_posts` SET `category` = 'City Hall' WHERE `category` = 'Hotel';

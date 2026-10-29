-- pixelrp: room for custom outfit colors. A custom color is stored in the
-- look as an 8-digit color id (16777216 + RGB) where a palette color takes
-- two to four, so a look with several custom pieces outgrows user_wardrobe's
-- varchar(120) and users' 255. Both go to varchar(512).
--
-- Each column keeps its own nullability, default, character set and
-- collation - read from information_schema and restated, since MODIFY
-- replaces the whole definition - and is only touched while it is shorter.

SET @sql = COALESCE((
  SELECT IF(CHARACTER_MAXIMUM_LENGTH < 512,
    CONCAT('ALTER TABLE `users` MODIFY `look` varchar(512)',
      ' CHARACTER SET ', CHARACTER_SET_NAME, ' COLLATE ', COLLATION_NAME,
      IF(IS_NULLABLE = 'NO', ' NOT NULL', ' NULL'),
      IF(COLUMN_DEFAULT IS NULL, '', CONCAT(' DEFAULT ', QUOTE(COLUMN_DEFAULT)))),
    'SELECT 1')
  FROM information_schema.COLUMNS
  WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'users' AND COLUMN_NAME = 'look'), 'SELECT 1');
PREPARE s FROM @sql; EXECUTE s; DEALLOCATE PREPARE s;

SET @sql = COALESCE((
  SELECT IF(CHARACTER_MAXIMUM_LENGTH < 512,
    CONCAT('ALTER TABLE `user_wardrobe` MODIFY `look` varchar(512)',
      ' CHARACTER SET ', CHARACTER_SET_NAME, ' COLLATE ', COLLATION_NAME,
      IF(IS_NULLABLE = 'NO', ' NOT NULL', ' NULL'),
      IF(COLUMN_DEFAULT IS NULL, '', CONCAT(' DEFAULT ', QUOTE(COLUMN_DEFAULT)))),
    'SELECT 1')
  FROM information_schema.COLUMNS
  WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'user_wardrobe' AND COLUMN_NAME = 'look'), 'SELECT 1');
PREPARE s FROM @sql; EXECUTE s; DEALLOCATE PREPARE s;

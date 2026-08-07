ALTER TABLE `users`
    ADD COLUMN `last_room_id` int UNSIGNED NOT NULL DEFAULT 0,
    ADD COLUMN `last_x` int NOT NULL DEFAULT 0,
    ADD COLUMN `last_y` int NOT NULL DEFAULT 0,
    ADD COLUMN `last_rot` int NOT NULL DEFAULT 0;

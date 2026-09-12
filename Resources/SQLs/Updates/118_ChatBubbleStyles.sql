-- pixelrp: every chat bubble the client can draw, known to the server too.
--
-- room_chat_styles is the REAL gate. ChatEvent, ShoutEvent, WhisperEvent and
-- SetChatStylePreferenceEvent all look the id up here and fall back to 0 when
-- it is missing, and :bubble refuses outright with a rank message - so a style
-- the client offers but this table has never heard of cannot be spoken in, no
-- matter who you are. It is not a rank problem; there is simply no row.
--
-- The table was seeded with 0-37 and never grew. That left 38 broken long
-- before this - it has been in ui-config the whole time - along with the two
-- custom styles at 40 and 41, and now the 85 ids the new collection adds.
--
-- Inserted with no required_right, matching what they were given in
-- ui-config.json. Existing rows are left exactly as they are: the ON DUPLICATE
-- clause is a no-op, so the mod_tool gate on 1, 2, 23, 30, 31, 33, 34, 35, 36
-- and 37 survives, names and all. To gate one of the new ones, set its
-- required_right to a permission the rank holds.
INSERT INTO `room_chat_styles` (`id`, `name`, `required_right`)
VALUES
       (0, '', ''), (1, '', ''), (2, '', ''), (3, '', ''), (4, '', ''), (5, '', ''), (6, '', ''), (7, '', ''),
       (8, '', ''), (9, '', ''), (10, '', ''), (11, '', ''), (12, '', ''), (13, '', ''), (14, '', ''), (15, '', ''),
       (16, '', ''), (17, '', ''), (18, '', ''), (19, '', ''), (20, '', ''), (21, '', ''), (22, '', ''), (23, '', ''),
       (24, '', ''), (25, '', ''), (26, '', ''), (27, '', ''), (28, '', ''), (29, '', ''), (30, '', ''), (31, '', ''),
       (32, '', ''), (33, '', ''), (34, '', ''), (35, '', ''), (36, '', ''), (37, '', ''), (38, '', ''), (39, '', ''),
       (40, '', ''), (41, '', ''), (42, '', ''), (43, '', ''), (44, '', ''), (45, '', ''), (46, '', ''), (47, '', ''),
       (48, '', ''), (49, '', ''), (50, '', ''), (51, '', ''), (52, '', ''), (53, '', ''), (54, '', ''), (55, '', ''),
       (56, '', ''), (57, '', ''), (58, '', ''), (59, '', ''), (60, '', ''), (61, '', ''), (62, '', ''), (63, '', ''),
       (64, '', ''), (65, '', ''), (66, '', ''), (67, '', ''), (68, '', ''), (69, '', ''), (70, '', ''), (71, '', ''),
       (72, '', ''), (73, '', ''), (74, '', ''), (75, '', ''), (76, '', ''), (77, '', ''), (78, '', ''), (79, '', ''),
       (80, '', ''), (81, '', ''), (82, '', ''), (83, '', ''), (84, '', ''), (85, '', ''), (86, '', ''), (87, '', ''),
       (88, '', ''), (89, '', ''), (90, '', ''), (91, '', ''), (92, '', ''), (93, '', ''), (94, '', ''), (95, '', ''),
       (96, '', ''), (97, '', ''), (98, '', ''), (99, '', ''), (100, '', ''), (101, '', ''), (102, '', ''), (103, '', ''),
       (104, '', ''), (105, '', ''), (106, '', ''), (107, '', ''), (108, '', ''), (109, '', ''), (110, '', ''), (111, '', ''),
       (112, '', ''), (113, '', ''), (114, '', ''), (115, '', ''), (116, '', ''), (117, '', ''), (118, '', ''), (119, '', ''),
       (120, '', ''), (121, '', ''), (122, '', ''), (123, '', ''), (124, '', ''), (143, '', '')
ON DUPLICATE KEY UPDATE `id` = `id`;

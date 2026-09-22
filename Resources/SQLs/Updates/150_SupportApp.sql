-- pixelrp: the Support app on the Phone.
--
-- A player opens a conversation; it goes into ONE queue and is offered to
-- staff in turn (round-robin). The player always talks to Trina - the same
-- newsroom byline the News app publishes anonymous stories under (70) - so
-- who actually answers, and the fact that it can change hands, never reaches
-- them. Staff see the real names throughout.
--
-- Three tables and no more: a thread, its messages, and the rotation.
--
-- WHY THE ROTATION IS A TABLE AND NOT A FIELD IN MEMORY. The pointer has to
-- survive a restart, or the hotel comes back up and starts the turn order
-- again from whoever happens to sort first - which over a week is a real
-- difference in who does the work. One row per staff member, carrying their
-- place in the order and when they were last offered a chat.

CREATE TABLE IF NOT EXISTS `rp_support_threads` (
  `id` int(11) NOT NULL AUTO_INCREMENT,
  `player_id` int(11) NOT NULL,
  -- Context for whoever answers, NOT a route: there is one pool and every
  -- category goes into it.
  `category` varchar(24) NOT NULL DEFAULT 'other',
  -- 'waiting'  in the queue, nobody has taken it
  -- 'offered'  held for one staff member until offered_until passes
  -- 'open'     claimed and being answered
  -- 'resolved' closed
  `status` varchar(12) NOT NULL DEFAULT 'waiting',
  -- The staff member answering, or being offered it. Never sent to the player.
  `staff_id` int(11) NOT NULL DEFAULT 0,
  -- When the current offer lapses and the chat moves to the next in line.
  `offered_until` int(11) NOT NULL DEFAULT 0,
  -- How many offers have lapsed on this thread, so one nobody wants can be
  -- told apart from one that simply arrived.
  `offers` int(11) NOT NULL DEFAULT 0,
  `created_at` int(11) NOT NULL,
  `updated_at` int(11) NOT NULL,
  `resolved_at` int(11) NOT NULL DEFAULT 0,
  PRIMARY KEY (`id`),
  -- The queue read: waiting threads, oldest first.
  KEY `queue` (`status`, `created_at`),
  -- A player's own list, and the "do you already have one open" check.
  KEY `player` (`player_id`, `status`),
  -- A staff member's open chats, and their cap.
  KEY `staff` (`staff_id`, `status`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

CREATE TABLE IF NOT EXISTS `rp_support_messages` (
  `id` int(11) NOT NULL AUTO_INCREMENT,
  `thread_id` int(11) NOT NULL,
  -- The real author, always. The player's copy is rendered as Trina by the
  -- composer; storing "Trina" here would throw away who actually said it,
  -- which is the one thing a moderation log needs.
  `author_id` int(11) NOT NULL,
  -- 0 the player, 1 staff. Cheaper than joining to work out which side a
  -- message came from, and correct even after a staff member is deleted.
  `from_staff` tinyint(1) NOT NULL DEFAULT 0,
  `body` varchar(1000) NOT NULL,
  `created_at` int(11) NOT NULL,
  PRIMARY KEY (`id`),
  KEY `thread` (`thread_id`, `id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

CREATE TABLE IF NOT EXISTS `rp_support_rotation` (
  `user_id` int(11) NOT NULL,
  -- Their own switch: off means skip, and they are never offered anything.
  `available` tinyint(1) NOT NULL DEFAULT 0,
  -- The turn order. Lowest last_offered_at among the available is next, so
  -- the pointer is DERIVED rather than stored - there is no single cursor to
  -- go stale when somebody joins, leaves or is removed mid-rotation.
  `last_offered_at` int(11) NOT NULL DEFAULT 0,
  -- Consecutive lapsed offers. Two and they are switched off automatically;
  -- answering anything clears it.
  `missed` int(11) NOT NULL DEFAULT 0,
  `updated_at` int(11) NOT NULL DEFAULT 0,
  PRIMARY KEY (`user_id`),
  KEY `next_up` (`available`, `last_offered_at`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

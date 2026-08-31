-- Player-run hiring: manage_rank_order is the minimum rank_order allowed to
-- use :hire / :fire for that corporation. Default 6 = the top two ranks of a
-- standard 7-rank ladder; PixelRP Leadership's short 3-rank ladder manages
-- from Management (3).

ALTER TABLE `rp_corporations`
  ADD COLUMN `manage_rank_order` int NOT NULL DEFAULT 6 AFTER `sort_order`;

UPDATE `rp_corporations` SET `manage_rank_order` = 3 WHERE `id` = 5;

-- The three highest ranks of any corporation are leadership roles
-- (Supervisor / Assistant Manager / General Manager style) and carry no
-- tiers. tiers = 0 marks a no-tier rank; employee tier 0 = untiered.

UPDATE `rp_corporation_ranks` r
JOIN (
  SELECT `corporation_id`, MAX(`rank_order`) AS max_order
  FROM `rp_corporation_ranks` GROUP BY `corporation_id`
) m ON m.`corporation_id` = r.`corporation_id`
SET r.`tiers` = 0
WHERE r.`rank_order` > (m.max_order - 3);

UPDATE `rp_corporation_employees` e
JOIN `rp_corporation_ranks` r ON r.`id` = e.`rank_id`
SET e.`tier` = 0
WHERE r.`tiers` = 0;

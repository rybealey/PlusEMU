-- Three Builders Club blocks take furni on top again (Twist: furni would not go
-- onto certain blocks, or be moved onto them).
--
-- Stacking was turned off on them in the Function tool in September (Yavn:
-- Small Block 13 and Marble Block 14 on the 12th, Small Block 14 on the 14th).
-- Every other colour of both blocks stacks - 68 of the Small Block, 14 of the
-- Marble Block - and the other blocks switched off that week (Large Block
-- 14/28, Round 14/28) were already switched back on 16 September; these three
-- were missed. Nothing else about them changes.
--
-- Only while they still have stacking off, so a later choice in the Function
-- tool stands.

UPDATE `furniture`
   SET `can_stack` = '1'
 WHERE `id` IN (65004, 65005, 65082)
   AND `item_name` IN ('bc_block_0*13', 'bc_block_0*14', 'bc_block_marble*14')
   AND `can_stack` = '0';

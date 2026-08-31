-- Commands resolve corporations by acronym now; corp_key is dead weight.

ALTER TABLE `rp_corporations` DROP COLUMN `corp_key`;

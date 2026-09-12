-- pixelrp: :openbank is retired - the tellers do this now.
--
-- 114 called it a bridge and said so in its own comment: accounts had moved
-- from the Wallet to a bank branch that did not exist yet, and without a staff
-- command there was no way to give anybody an account at all. 120 and 121 built
-- the branch. A banker bot opens an account in person, on request, within two
-- tiles of the counter, which is the thing the bridge was standing in for.
--
-- The command class is deleted in the same commit. Commands are discovered by
-- assembly scan rather than a registration list, so the file going away is the
-- whole removal on the emulator side; this row is what would otherwise be left
-- behind, granting a permission for a command that no longer answers.
--
-- Idempotent: deleting a row that is already gone is not an error.

DELETE FROM `permissions_commands` WHERE `command` = 'command_openbank';

-- Should return nothing.
SELECT `command`, `group_id` FROM `permissions_commands` WHERE `command` = 'command_openbank';

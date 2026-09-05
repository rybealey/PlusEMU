using Dapper;
using Plus.Core;
using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Rooms.Movement;

namespace Plus.HabboHotel.Rooms.Chat.Commands.Administrator;

/// <summary>
/// pixelrp Movement V2: live in-game toggle.
///
///   :movementv2          show the current state
///   :movementv2 on       V2 owns route + timing for human users
///   :movementv2 off      V1 owns everything again
///
/// WHY THIS EXISTS: before this, flipping the flag OR rolling it back needed a
/// database patch and a full beta deploy - minutes of downtime with a broken
/// movement system live, and it needed someone with deploy access. The first
/// beta test froze every avatar, and the only way out was a deploy. This makes
/// rollback instant and puts it in the hands of whoever is actually testing.
///
/// Writes the row AND reloads the in-memory settings cache, because
/// MovementSettings.Enabled reads through SettingsManager.TryGetValue, which is
/// served from a dictionary populated at boot.
///
/// Reuses command_update (rank 5+) rather than inventing a new permission,
/// which would need a permissions_commands row to exist before the command
/// could ever run.
/// </summary>
internal class MovementV2Command : IChatCommand
{
    public string Key => "movementv2";
    public string PermissionRequired => "command_update";
    public string Parameters => "[on|off]";
    public string Description => "Toggle Movement V2 (route + timing for human users) live, without a deploy.";

    public void Execute(GameClient session, Room room, string[] parameters)
    {
        if (parameters == null || parameters.Length == 0)
        {
            session.SendWhisper(
                $"Movement V2 is currently {(MovementSettings.Enabled ? "ON" : "OFF")}. " +
                "Use :movementv2 on / :movementv2 off.");
            return;
        }

        var arg = parameters[0].ToLower();
        bool enable;
        switch (arg)
        {
            case "on":
            case "1":
            case "enable":
                enable = true;
                break;
            case "off":
            case "0":
            case "disable":
                enable = false;
                break;
            default:
                session.SendWhisper("Usage: :movementv2 on | :movementv2 off.");
                return;
        }

        var value = enable ? "1" : "0";
        try
        {
            using (var dbClient = PlusEnvironment.DatabaseManager.Connection())
            {
                dbClient.Execute(
                    "INSERT INTO `server_settings` (`key`, `value`, `description`) " +
                    "VALUES (@key, @value, @description) " +
                    "ON DUPLICATE KEY UPDATE `value` = @value",
                    new
                    {
                        key = MovementSettings.EnabledKey,
                        value,
                        description = "Movement V2: 1 = V2 owns route + timing for human users (bots/pets stay on V1). Anything else = off."
                    });
            }

            // Refresh the cache TryGetValue reads from, or the write would not
            // take effect until the next emulator restart.
            PlusEnvironment.SettingsManager.Reload().GetAwaiter().GetResult();
        }
        catch (Exception e)
        {
            ExceptionLogger.LogException(e);
            session.SendWhisper("Failed to change Movement V2 - check the emulator log.");
            return;
        }

        var confirmed = MovementSettings.Enabled;
        if (confirmed != enable)
        {
            session.SendWhisper(
                $"Movement V2 write did not take effect (still {(confirmed ? "ON" : "OFF")}). Check the emulator log.");
            return;
        }

        if (enable)
            session.SendWhisper(
                "Movement V2 is now ON for human users. Walk out of the room and back in to be enrolled " +
                "- players already standing in a room stay on V1 until they re-enter.");
        else
            session.SendWhisper(
                "Movement V2 is now OFF. V1 reclaims each player as they move; no restart needed.");
    }
}

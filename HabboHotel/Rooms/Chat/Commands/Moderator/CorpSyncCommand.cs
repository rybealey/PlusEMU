using Dapper;
using Plus.HabboHotel.Corporations;
using Plus.HabboHotel.GameClients;

namespace Plus.HabboHotel.Rooms.Chat.Commands.Moderator;

/// <summary>
/// pixelrp: :corpsync [acronym] - staff re-broadcast of employment for one
/// corporation's employees (or every corporation with no argument). Run it
/// after editing corp data directly in the database (badge, name, acronym,
/// rank names) so every online client updates in real-time; on-duty workers
/// also get their working motto rebuilt from the fresh data.
/// </summary>
internal class CorpSyncCommand : IChatCommand
{
    public string Key => "corpsync";
    public string PermissionRequired => "command_corpsync";

    public string Parameters => "%acronym%";

    public string Description => "Re-broadcast corporation employment data hotel-wide.";

    public void Execute(GameClient session, Room room, string[] parameters)
    {
        var corpId = 0;
        if (parameters.Any())
        {
            using var connection = PlusEnvironment.DatabaseManager.Connection();
            corpId = connection.QuerySingleOrDefault<int>(
                "SELECT `id` FROM `rp_corporations` WHERE UPPER(`acronym`) = @key LIMIT 1", new { key = parameters[0].ToUpperInvariant() });
            if (corpId == 0)
            {
                session.SendWhisper($"No corporation with the acronym '{parameters[0]}'.");
                return;
            }
        }
        var synced = CorporationUtility.BroadcastAllEmployments(corpId);
        session.SendWhisper($"Synced employment for {synced} employee{(synced == 1 ? "" : "s")}.");
    }
}

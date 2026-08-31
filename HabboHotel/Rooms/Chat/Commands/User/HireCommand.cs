using Dapper;
using Plus.HabboHotel.Corporations;
using Plus.HabboHotel.GameClients;

namespace Plus.HabboHotel.Rooms.Chat.Commands.User;

/// <summary>
/// pixelrp: :hire &lt;username&gt; - corporation leadership hires a player into
/// their OWN corporation at its lowest rank. Gated by RequireManager (at or
/// above the corp's manage_rank_order, clocked in). Refuses targets who
/// already hold a job anywhere - poaching stays a staff superhire power.
/// The new hire must be online (they should be present to take the job).
/// </summary>
internal class HireCommand : IChatCommand
{
    public string Key => "hire";
    public string PermissionRequired => "";

    public string Parameters => "%username%";

    public string Description => "Hire a player into your corporation.";

    public void Execute(GameClient session, Room room, string[] parameters)
    {
        if (!parameters.Any())
        {
            session.SendWhisper("Usage: :hire <username>");
            return;
        }
        var context = CorporationUtility.RequireManager(session);
        if (context == null)
            return;
        var target = PlusEnvironment.Game.ClientManager.GetClientByUsername(parameters[0]);
        if (target?.GetHabbo() == null)
        {
            session.SendWhisper($"{parameters[0]} isn't online - new hires must be here to take the job.");
            return;
        }
        var targetId = target.GetHabbo().Id;
        if (targetId == session.GetHabbo().Id)
        {
            session.SendWhisper("You already work here.");
            return;
        }
        var existing = CorporationUtility.GetEmployment(targetId);
        if (existing != null && existing.CorpId != 0)
        {
            session.SendWhisper($"{target.GetHabbo().Username} already works for {existing.CorpName} - they must quit or be fired first.");
            return;
        }
        int rankId;
        int tier;
        string rankName;
        using (var connection = PlusEnvironment.DatabaseManager.Connection())
        {
            var rank = connection.QuerySingleOrDefault<(int Id, string Name, int Tiers)>(
                "SELECT `id`, `name`, `tiers` FROM `rp_corporation_ranks` WHERE `corporation_id` = @corpId ORDER BY `rank_order` LIMIT 1",
                new { corpId = context.CorpId });
            rankId = rank.Id;
            rankName = rank.Name;
            tier = (rank.Tiers > 0) ? 1 : 0;
            connection.Execute(
                "INSERT INTO `rp_corporation_employees` (`user_id`, `corporation_id`, `rank_id`, `tier`, `hired_at`) " +
                "VALUES (@userId, @corpId, @rankId, @tier, UNIX_TIMESTAMP())",
                new { userId = targetId, corpId = context.CorpId, rankId, tier });
        }
        CorporationUtility.BroadcastEmployment(targetId);

        var actorRoomUser = session.GetHabbo().CurrentRoom?.GetRoomUserManager()?.GetRoomUserByHabbo(session.GetHabbo().Id);
        actorRoomUser?.OnChat(23, $"*has hired {target.GetHabbo().Username} at {context.CorpName}*", true);
        var targetRoomUser = target.GetHabbo().CurrentRoom?.GetRoomUserManager()?.GetRoomUserByHabbo(targetId);
        if (targetRoomUser != null)
            targetRoomUser.OnChat(4, $"*has been hired at {context.CorpName} as {rankName}*", true);
        else
            target.SendWhisper($"You've been hired at {context.CorpName} as {rankName}!");
    }
}

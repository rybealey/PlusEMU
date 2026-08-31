using Dapper;
using Plus.HabboHotel.Corporations;
using Plus.HabboHotel.GameClients;

namespace Plus.HabboHotel.Rooms.Chat.Commands.User;

/// <summary>
/// pixelrp: :fire &lt;username&gt; - corporation leadership fires an employee of
/// their OWN corporation. Gated by RequireManager; the target must be BELOW
/// the actor's rank and may be offline. The employee row is deleted, so all
/// shift data (weekly, lifetime, pay progress) dies with it - a rehire
/// starts from zero - and the corpId-0 broadcast clears clients in
/// real-time.
/// </summary>
internal class FireCommand : IChatCommand
{
    public string Key => "fire";
    public string PermissionRequired => "";

    public string Parameters => "%username%";

    public string Description => "Fire an employee from your corporation.";

    public void Execute(GameClient session, Room room, string[] parameters)
    {
        if (!parameters.Any())
        {
            session.SendWhisper("Usage: :fire <username>");
            return;
        }
        var context = CorporationUtility.RequireManager(session);
        if (context == null)
            return;
        var target = CorporationUtility.ResolveUser(parameters[0]);
        if (target == null)
        {
            session.SendWhisper($"No player named '{parameters[0]}'.");
            return;
        }
        if (target.Id == session.GetHabbo().Id)
        {
            session.SendWhisper("You can't fire yourself - use :quitjob.");
            return;
        }
        int targetRankOrder;
        using (var connection = PlusEnvironment.DatabaseManager.Connection())
        {
            targetRankOrder = connection.QuerySingleOrDefault<int>(
                "SELECT r.`rank_order` FROM `rp_corporation_employees` e " +
                "INNER JOIN `rp_corporation_ranks` r ON r.`id` = e.`rank_id` " +
                "WHERE e.`user_id` = @userId AND e.`corporation_id` = @corpId LIMIT 1",
                new { userId = target.Id, corpId = context.CorpId });
            if (targetRankOrder == 0)
            {
                session.SendWhisper($"{target.Username} doesn't work for {context.CorpName}.");
                return;
            }
            if (targetRankOrder >= context.RankOrder)
            {
                session.SendWhisper($"You can't fire someone at or above your own rank.");
                return;
            }
            // end any live shift first (banks and pays what was earned),
            // then the row - and every shift counter in it - is deleted
            ShiftManager.InterruptForDisconnect(target.Id);
            connection.Execute("DELETE FROM `rp_corporation_employees` WHERE `user_id` = @userId LIMIT 1", new { userId = target.Id });
        }
        CorporationUtility.BroadcastEmployment(target.Id);

        var actorRoomUser = session.GetHabbo().CurrentRoom?.GetRoomUserManager()?.GetRoomUserByHabbo(session.GetHabbo().Id);
        actorRoomUser?.OnChat(23, $"*has fired {target.Username} from {context.CorpName}*", true);
        var targetRoomUser = target.Client?.GetHabbo()?.CurrentRoom?.GetRoomUserManager()?.GetRoomUserByHabbo(target.Id);
        if (targetRoomUser != null)
            targetRoomUser.OnChat(4, $"*has been fired from {context.CorpName}*", true);
        else
            target.Client?.SendWhisper($"You've been fired from {context.CorpName}.");
    }
}

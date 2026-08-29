using Dapper;
using Plus.HabboHotel.Corporations;
using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Users;

namespace Plus.HabboHotel.Rooms.Chat.Commands.Moderator;

/// <summary>
/// pixelrp: staff-only firing from any corporation: :superfire &lt;username&gt;.
/// Clears the target's employment and broadcasts the change in real-time
/// (RpUserCorpComposer with corpId 0 empties infostand corp slots and
/// profile rows). Announced with the same theatrical shouts as :superhire -
/// staff bubble 23, target bubble 4.
/// </summary>
internal class SuperFireCommand : ITargetChatCommand
{
    public string Key => "superfire";
    public string PermissionRequired => "command_superfire";

    public string Parameters => "%username%";

    public string Description => "Fire a player from their corporation.";

    public bool MustBeInSameRoom => false;

    public Task Execute(GameClient session, Room room, Habbo target, string[] parameters)
    {
        var employment = CorporationUtility.GetEmployment(target.Id);
        if (employment == null || employment.CorpId == 0)
        {
            session.SendWhisper($"{target.Username} isn't employed by any corporation.");
            return Task.CompletedTask;
        }
        using (var connection = PlusEnvironment.DatabaseManager.Connection())
        {
            connection.Execute("DELETE FROM `rp_corporation_employees` WHERE `user_id` = @userId LIMIT 1", new { userId = target.Id });
        }

        // Real-time clear: the target's room (infostand corp slots), the
        // target themself, and the firing staff member's client.
        var composer = CorporationUtility.ComposeFor(target.Id, null);
        var targetRoom = target.CurrentRoom;
        if (targetRoom != null)
            targetRoom.SendPacket(composer);
        else
            target.Client?.Send(composer);
        if (session.GetHabbo().CurrentRoom?.Id != targetRoom?.Id)
            session.Send(composer);

        var staffRoomUser = session.GetHabbo().CurrentRoom?.GetRoomUserManager()?.GetRoomUserByHabbo(session.GetHabbo().Id);
        staffRoomUser?.OnChat(23, $"*has fired {target.Username} from {employment.CorpName}*", true);
        var targetRoomUser = targetRoom?.GetRoomUserManager()?.GetRoomUserByHabbo(target.Id);
        if (targetRoomUser != null)
            targetRoomUser.OnChat(4, $"*has been fired from {employment.CorpName}*", true);
        else
            target.Client?.SendWhisper($"You've been fired from {employment.CorpName}.");
        return Task.CompletedTask;
    }
}

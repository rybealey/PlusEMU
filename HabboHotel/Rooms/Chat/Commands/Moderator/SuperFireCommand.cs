using Dapper;
using Plus.HabboHotel.Corporations;
using Plus.HabboHotel.GameClients;

namespace Plus.HabboHotel.Rooms.Chat.Commands.Moderator;

/// <summary>
/// pixelrp: staff-only firing from any corporation: :superfire &lt;username&gt;.
/// Deletes the target's employment row (all shift data with it) and
/// broadcasts the corpId-0 clear hotel-wide. The target does NOT need to be
/// online; announcements simply skip an offline player.
/// </summary>
internal class SuperFireCommand : IChatCommand
{
    public string Key => "superfire";
    public string PermissionRequired => "command_superfire";

    public string Parameters => "%username%";

    public string Description => "Fire a player from their corporation.";

    public void Execute(GameClient session, Room room, string[] parameters)
    {
        if (!parameters.Any())
        {
            session.SendWhisper("Usage: :superfire <username>");
            return;
        }
        var target = CorporationUtility.ResolveUser(parameters[0]);
        if (target == null)
        {
            session.SendWhisper($"No player named '{parameters[0]}'.");
            return;
        }
        var employment = CorporationUtility.GetEmployment(target.Id);
        if (employment == null || employment.CorpId == 0)
        {
            session.SendWhisper($"{target.Username} isn't employed by any corporation.");
            return;
        }
        // end any live shift first - banks progress and clears on_duty
        ShiftManager.InterruptForDisconnect(target.Id);

        using (var connection = PlusEnvironment.DatabaseManager.Connection())
        {
            connection.Execute("DELETE FROM `rp_corporation_employees` WHERE `user_id` = @userId LIMIT 1", new { userId = target.Id });
        }

        // Real-time clear: hotel-wide broadcast (corp windows, profiles,
        // infostands everywhere). Shift data died with the row.
        CorporationUtility.BroadcastEmployment(target.Id);

        var staffRoomUser = session.GetHabbo().CurrentRoom?.GetRoomUserManager()?.GetRoomUserByHabbo(session.GetHabbo().Id);
        staffRoomUser?.OnChat(23, $"*has fired {target.Username} from {employment.CorpName}*", true);
        var targetRoomUser = target.Client?.GetHabbo()?.CurrentRoom?.GetRoomUserManager()?.GetRoomUserByHabbo(target.Id);
        if (targetRoomUser != null)
            targetRoomUser.OnChat(4, $"*has been fired from {employment.CorpName}*", true);
        else
            target.Client?.SendWhisper($"You've been fired from {employment.CorpName}.");
    }
}

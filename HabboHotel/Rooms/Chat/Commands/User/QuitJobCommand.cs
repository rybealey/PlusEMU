using Dapper;
using Plus.HabboHotel.Corporations;
using Plus.HabboHotel.GameClients;

namespace Plus.HabboHotel.Rooms.Chat.Commands.User;

/// <summary>
/// pixelrp: :quitjob - resign from your corporation. Works on or off duty
/// (a live shift is ended first, banking and paying what was earned). The
/// employee row is deleted, so all shift data dies with it - a rehire
/// starts from zero - and the corpId-0 broadcast clears clients in
/// real-time. Announces with the blue shout bubble.
/// </summary>
internal class QuitJobCommand : IChatCommand
{
    public string Key => "quitjob";
    public string PermissionRequired => "";

    public string Parameters => "";

    public string Description => "Resign from your corporation.";

    public void Execute(GameClient session, Room room, string[] parameters)
    {
        var userId = session.GetHabbo().Id;
        var employment = CorporationUtility.GetEmployment(userId);
        if (employment == null || employment.CorpId == 0)
        {
            session.SendWhisper("You don't have a job to quit.");
            return;
        }
        // ends a live shift first (banks + pays what was earned, reverts the
        // working motto), then the row and its shift data are deleted
        ShiftManager.InterruptForDisconnect(userId);
        using (var connection = PlusEnvironment.DatabaseManager.Connection())
        {
            connection.Execute("DELETE FROM `rp_corporation_employees` WHERE `user_id` = @userId LIMIT 1", new { userId });
        }
        CorporationUtility.BroadcastEmployment(userId);

        var roomUser = session.GetHabbo().CurrentRoom?.GetRoomUserManager()?.GetRoomUserByHabbo(userId);
        roomUser?.OnChat(4, $"*has resigned from their role at {employment.CorpName}*", true);
    }
}

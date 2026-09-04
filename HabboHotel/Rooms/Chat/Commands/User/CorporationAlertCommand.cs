using Dapper;
using Plus.HabboHotel.Corporations;
using Plus.HabboHotel.GameClients;

namespace Plus.HabboHotel.Rooms.Chat.Commands.User;

/// <summary>
/// pixelrp: :ca &lt;message&gt; - whisper a message to the sender's corporation
/// as "[sender]: message". Strictly on the clock: the sender must be on duty
/// to send, and only employees currently on duty receive it.
/// </summary>
internal class CorporationAlertCommand : IChatCommand
{
    private readonly IGameClientManager _gameClientManager;

    public string Key => "ca";
    public string PermissionRequired => "";

    public string Parameters => "%message%";

    public string Description => "Send an alert to your corporation's on-duty employees (you must be clocked in).";

    public CorporationAlertCommand(IGameClientManager gameClientManager)
    {
        _gameClientManager = gameClientManager;
    }

    public void Execute(GameClient session, Room room, string[] parameters)
    {
        var habbo = session.GetHabbo();
        var employment = CorporationUtility.GetEmployment(habbo.Id);
        if (employment == null || employment.CorpId == 0)
        {
            session.SendWhisper("You don't work for a corporation.");
            return;
        }
        if (!ShiftManager.IsOnDuty(habbo.Id))
        {
            session.SendWhisper("You must be clocked in to send a corporation alert.");
            return;
        }
        var message = CommandManager.MergeParams(parameters);
        if (string.IsNullOrWhiteSpace(message))
        {
            session.SendWhisper("Usage: :ca <message>");
            return;
        }

        List<int> employeeIds;
        using (var connection = PlusEnvironment.DatabaseManager.Connection())
        {
            employeeIds = connection.Query<int>(
                "SELECT `user_id` FROM `rp_corporation_employees` WHERE `corporation_id` = @corpId",
                new { corpId = employment.CorpId }).ToList();
        }

        var line = $"[{habbo.Username}]: {message}";
        foreach (var userId in employeeIds)
        {
            if (!ShiftManager.IsOnDuty(userId))
                continue;
            var client = _gameClientManager.GetClientByUserId(userId);
            if (client?.GetHabbo() != null)
                client.SendWhisper(line);
        }
    }
}

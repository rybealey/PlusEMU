using Plus.Communication.Packets.Outgoing.Rooms.Chat;
using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Gangs;

namespace Plus.HabboHotel.Rooms.Chat.Commands.User;

/// <summary>
/// pixelrp: :ga &lt;message&gt; - whisper a message to every online member of
/// the sender's gang, wherever they are in the hotel, as "[sender]: message".
/// The sender gets the same line back as their receipt.
/// </summary>
internal class GangAlertCommand : IChatCommand
{
    // gang alerts get their own bubble (corporation alerts use 11)
    private const int AlertBubble = 12;

    private readonly IGameClientManager _gameClientManager;

    public string Key => "ga";
    public string PermissionRequired => "";

    public string Parameters => "%message%";

    public string Description => "Send an alert to everyone in your gang.";

    public GangAlertCommand(IGameClientManager gameClientManager)
    {
        _gameClientManager = gameClientManager;
    }

    public void Execute(GameClient session, Room room, string[] parameters)
    {
        var habbo = session.GetHabbo();
        var gang = GangUtility.GetGang(habbo.Id);
        if (gang == null)
        {
            session.SendWhisper("You're not in a gang.");
            return;
        }
        var message = CommandManager.MergeParams(parameters);
        if (string.IsNullOrWhiteSpace(message))
        {
            session.SendWhisper("Usage: :ga <message>");
            return;
        }

        var line = $"[{habbo.Username}]: {message}";
        // the alert went out - the sender's chat box keeps the prefix for the next one
        session.Send(new RpRetainChatPrefixComposer(":ga"));
        foreach (var member in GangManager.GetMembers(gang.GangId))
        {
            var client = _gameClientManager.GetClientByUserId(member.UserId);
            if (client?.GetHabbo() != null)
                client.SendWhisper(line, AlertBubble);
        }
    }
}

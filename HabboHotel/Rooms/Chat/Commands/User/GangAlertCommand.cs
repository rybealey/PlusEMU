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
    // Gang alerts get their own GREEN bubble, and a private id for it.
    //
    // 12 was pink and, more to the point, a style any player can pick for
    // themselves - room_chat_styles has a row for it with no required right.
    // Sharing it meant a gang alert looked like anyone else's chat, and left
    // Chat History unable to tell the two apart. 200 has no row at all, so
    // nobody can select it; server-sent whispers do not consult that table.
    // (Corporation alerts still use 11.)
    private const int AlertBubble = 200;

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

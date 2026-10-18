using Plus.Communication.Packets.Outgoing.Rooms.Chat;
using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Users.Relationships;

namespace Plus.HabboHotel.Rooms.Chat.Commands.User.Social;

/// <summary>
/// pixelrp: :pa &lt;message&gt; - partner chat. Whispers the message to the
/// sender's partner (the :propose partnership), wherever they are in the
/// hotel, as "[sender]: message" - :ga's shape for a gang of two. The sender
/// gets the same line back as their receipt.
/// </summary>
internal class PartnerAlertCommand : IChatCommand
{
    // Partner chat's own bubble, private like the gang alert's 200: it has no
    // room_chat_styles row, so no player can pick it or speak in it, and the
    // client can tell a partner line by the bubble alone. Drawn from the pink
    // heart bubble 16 (RpChatBubbleStyles.scss).
    private const int AlertBubble = 202;

    private readonly IGameClientManager _gameClientManager;

    public string Key => "pa";
    public string PermissionRequired => "";

    public string Parameters => "%message%";

    public string Description => "Send a message to your partner.";

    public PartnerAlertCommand(IGameClientManager gameClientManager)
    {
        _gameClientManager = gameClientManager;
    }

    public void Execute(GameClient session, Room room, string[] parameters)
    {
        var habbo = session.GetHabbo();
        var partnerId = PartnershipUtility.PartnerOf(habbo.Id);
        if (partnerId == 0)
        {
            session.SendWhisper("You don't have a partner.");
            session.Send(RpRetainChatPrefixComposer.Drop());
            return;
        }
        var message = CommandManager.MergeParams(parameters);
        if (string.IsNullOrWhiteSpace(message))
        {
            session.SendWhisper("Usage: :pa <message>");
            session.Send(RpRetainChatPrefixComposer.Drop());
            return;
        }
        var partner = _gameClientManager.GetClientByUserId(partnerId);
        if (partner?.GetHabbo() == null)
        {
            session.SendWhisper("Your partner is offline.");
            session.Send(RpRetainChatPrefixComposer.Drop());
            return;
        }

        var line = $"[{habbo.Username}]: {message}";
        // the message went out - the sender's chat box keeps the prefix for the next one
        session.Send(new RpRetainChatPrefixComposer(":pa"));
        partner.SendWhisper(line, AlertBubble);
        session.SendWhisper(line, AlertBubble);
    }
}

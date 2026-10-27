using Plus.HabboHotel.Corporations;
using Plus.Communication.Packets.Outgoing.Rooms.Chat;
using Plus.HabboHotel.GameClients;

namespace Plus.HabboHotel.Rooms.Chat.Commands.User;

/// <summary>
/// pixelrp: :ca &lt;message&gt; - whisper a message to the sender's corporation
/// as "[sender]: message". Strictly on the clock: the sender must be on duty
/// to send, and only employees currently on duty receive it.
/// </summary>
internal class CorporationAlertCommand : IChatCommand
{
    // corporation alerts get their own bubble so they read apart from gang
    // alerts and plain system whispers
    private const int AlertBubble = 11;

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
        // pixelrp (Twist, 2026-10-27): from the shifts in memory - the sender
        // must be clocked in, and the alert goes to whoever is clocked in for
        // the same corporation, so the shifts say both. Every line used to read
        // the sender's job and the corporation's whole staff list from the
        // database. The job is only read to say why an off-duty sender can't.
        var corpId = ShiftManager.CorpOf(habbo.Id);
        if (corpId == 0)
        {
            var employment = CorporationUtility.GetEmployment(habbo.Id);
            session.SendWhisper(employment == null || employment.CorpId == 0
                ? "You don't work for a corporation."
                : "You must be clocked in to send a corporation alert.");
            session.Send(RpRetainChatPrefixComposer.Drop());
            return;
        }
        var message = CommandManager.MergeParams(parameters);
        if (string.IsNullOrWhiteSpace(message))
        {
            session.SendWhisper("Usage: :ca <message>");
            session.Send(RpRetainChatPrefixComposer.Drop());
            return;
        }

        var line = $"[{habbo.Username}]: {message}";
        // the alert went out - the sender's chat box keeps the prefix for the next one
        session.Send(new RpRetainChatPrefixComposer(":ca"));
        foreach (var (userId, corp, _) in ShiftManager.OnDuty())
        {
            if (corp != corpId)
                continue;
            var client = _gameClientManager.GetClientByUserId(userId);
            if (client?.GetHabbo() != null)
                client.SendWhisper(line, AlertBubble);
        }
    }
}

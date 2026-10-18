using Plus.Communication.Packets.Outgoing.Rooms.Notifications;
using Plus.HabboHotel.GameClients;

namespace Plus.HabboHotel.Rooms.Chat.Commands.Moderator;

internal class HotelAlertCommand : IChatCommand
{
    private readonly IGameClientManager _gameClientManager;
    public string Key => "ha";
    public string PermissionRequired => "command_hotel_alert";

    public string Parameters => "%message%";

    public string Description => "Send a notification to the entire hotel. Links in it can be clicked.";

    public HotelAlertCommand(IGameClientManager gameClientManager)
    {
        _gameClientManager = gameClientManager;
    }

    public void Execute(GameClient session, Room room, string[] parameters)
    {
        if (!parameters.Any())
        {
            session.SendWhisper("Please enter a message to send.");
            return;
        }
        var message = CommandManager.MergeParams(parameters);
        // pixelrp: hotel alerts render as the client's INFO notification bubble
        // (display=BUBBLE path in useNotification), not the modal popup. Any
        // http(s):// or www. address in the message is drawn as a link there
        // (NotificationPlatformBubbleView), which is why :hal is gone (Twist,
        // 2026-10-18).
        _gameClientManager.SendPacket(new RoomNotificationComposer("hotel.alert",
            new Dictionary<string, string> { { "display", "BUBBLE" }, { "message", message } }));
    }
}
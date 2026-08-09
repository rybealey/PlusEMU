using Plus.Communication.Packets.Outgoing.Rooms.Notifications;
using Plus.HabboHotel.GameClients;

namespace Plus.HabboHotel.Rooms.Chat.Commands.Moderator;

internal class HotelAlertCommand : IChatCommand
{
    private readonly IGameClientManager _gameClientManager;
    public string Key => "ha";
    public string PermissionRequired => "command_hotel_alert";

    public string Parameters => "%message%";

    public string Description => "Send a bubble notification to the entire hotel.";

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
        // (display=BUBBLE path in useNotification), not the modal popup.
        _gameClientManager.SendPacket(new RoomNotificationComposer("hotel.alert",
            new Dictionary<string, string> { { "display", "BUBBLE" }, { "message", message } }));
    }
}
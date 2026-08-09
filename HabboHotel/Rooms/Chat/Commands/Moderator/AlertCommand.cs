using Plus.Communication.Packets.Outgoing.Rooms.Notifications;
using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Users;

namespace Plus.HabboHotel.Rooms.Chat.Commands.Moderator;

internal class AlertCommand : ITargetChatCommand
{
    public string Key => "alert";
    public string PermissionRequired => "command_alert_user";

    public string Parameters => "%username% %Messages%";

    public string Description => "Alert a user with a bubble notification.";

    public bool MustBeInSameRoom => false;

    public Task Execute(GameClient session, Room room, Habbo habbo, string[] parameters)
    {
        // pixelrp: self-alerts are allowed — staff use them to preview the toast.
        var message = CommandManager.MergeParams(parameters);
        // pixelrp: delivered as the client's Moderation toast (red-tinted
        // Platform bubble), not the modal popup.
        habbo.Client.Send(new RoomNotificationComposer("mod.alert",
            new Dictionary<string, string> { { "display", "BUBBLE" }, { "message", message } }));
        session.SendWhisper($"Alert successfully sent to {habbo.Username}");
        return Task.CompletedTask;
    }
}
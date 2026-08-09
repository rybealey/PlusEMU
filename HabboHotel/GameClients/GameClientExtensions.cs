using Plus.Communication.Packets.Outgoing.Moderation;
using Plus.Communication.Packets.Outgoing.Rooms.Chat;
using Plus.Communication.Packets.Outgoing.Rooms.Notifications;

namespace Plus.HabboHotel.GameClients;

public static class GameClientExtensions
{
    public static void SendWhisper(this GameClient client, string message, int colour = 0)
    {
        if (client.GetHabbo() == null || client.GetHabbo().CurrentRoom == null)
            return;
        var user = client.GetHabbo().CurrentRoom?.GetRoomUserManager().GetRoomUserByHabbo(client.GetHabbo().Username);
        if (user == null)
            return;
        client.Send(new WhisperComposer(user.VirtualId, message, 0, colour == 0 ? user.LastBubble : colour));
    }

    public static void SendNotification(this GameClient client, string message) => client.Send(new BroadcastMessageAlertComposer(message));

    // pixelrp: user-facing moderation alerts render as the client's persistent
    // red Moderation toast (mod.alert, display=BUBBLE), not the modal popup.
    public static void SendModerationAlert(this GameClient client, string message) => client.Send(new RoomNotificationComposer("mod.alert",
        new Dictionary<string, string> { { "display", "BUBBLE" }, { "message", message } }));
}
using Plus.Communication.Packets.Outgoing.Moderation;
using Plus.Communication.Packets.Outgoing.Rooms.Chat;
using Plus.Communication.Packets.Outgoing.Rooms.Notifications;

namespace Plus.HabboHotel.GameClients;

public static class GameClientExtensions
{
    /// <summary>
    /// Bubble style every system whisper uses. Fixed rather than the player's
    /// own LastBubble, so a message from the hotel is always visually distinct
    /// from their chat and reads the same for everyone.
    /// </summary>
    private const int SystemWhisperBubble = 1;

    /// <param name="colour">
    /// Bubble style override. 0 (the default) means "system", which is
    /// <see cref="SystemWhisperBubble" /> - NOT the player's own bubble.
    /// </param>
    public static void SendWhisper(this GameClient client, string message, int colour = 0)
    {
        if (client.GetHabbo() == null || client.GetHabbo().CurrentRoom == null)
            return;
        var user = client.GetHabbo().CurrentRoom?.GetRoomUserManager().GetRoomUserByHabbo(client.GetHabbo().Username);
        if (user == null)
            return;
        client.Send(new WhisperComposer(user.VirtualId, message, 0, colour == 0 ? SystemWhisperBubble : colour));
    }

    public static void SendNotification(this GameClient client, string message) => client.Send(new BroadcastMessageAlertComposer(message));

    // pixelrp: user-facing moderation alerts render as the client's persistent
    // red Moderation toast (mod.alert, display=BUBBLE), not the modal popup.
    public static void SendModerationAlert(this GameClient client, string message) => client.Send(new RoomNotificationComposer("mod.alert",
        new Dictionary<string, string> { { "display", "BUBBLE" }, { "message", message } }));
}
using System.Text.RegularExpressions;
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

    /// <summary>
    /// Bubble style for a system message that used to be a modal popup: the
    /// hotel's ephemeral "notice" bubble (34), whispered so only the player
    /// sees it and it fades like chat.
    /// </summary>
    private const int SystemNoticeBubble = 34;

    /// <summary>
    /// A one-off message from the hotel to the player. In a room it arrives as
    /// an ephemeral bubble-34 whisper over their head; outside a room (login,
    /// navigator) it falls back to the classic popup. Use
    /// <see cref="SendPopup" /> when the message must survive leaving the room
    /// or is a multi-line report.
    /// </summary>
    public static void SendNotification(this GameClient client, string message)
    {
        var habbo = client.GetHabbo();
        var user = habbo?.CurrentRoom?.GetRoomUserManager().GetRoomUserByHabbo(habbo.Username);
        if (user == null)
        {
            client.SendPopup(message);
            return;
        }
        var flat = Regex.Replace(message ?? string.Empty, @"[\r\n]+", " ").Trim();
        client.Send(new WhisperComposer(user.VirtualId, flat, 0, SystemNoticeBubble));
    }

    /// <summary>The classic modal alert. Only for messages a bubble can't carry.</summary>
    public static void SendPopup(this GameClient client, string message) => client.Send(new BroadcastMessageAlertComposer(message));

    // pixelrp: user-facing moderation alerts render as the client's persistent
    // red Moderation toast (mod.alert, display=BUBBLE), not the modal popup.
    public static void SendModerationAlert(this GameClient client, string message) => client.Send(new RoomNotificationComposer("mod.alert",
        new Dictionary<string, string> { { "display", "BUBBLE" }, { "message", message } }));
}
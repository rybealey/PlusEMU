using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Rooms.Chat.Styles;

namespace Plus.Communication.Packets.Incoming.Preferences;

internal class SetChatStylePreferenceEvent : IPacketEvent
{
    private readonly IChatStyleManager _chatStyleManager;

    public SetChatStylePreferenceEvent(IChatStyleManager chatStyleManager)
    {
        _chatStyleManager = chatStyleManager;
    }

    public Task Parse(GameClient session, IIncomingPacket packet)
    {
        var chatBubbleId = packet.ReadInt();

        // pixelrp: validate the bubble against room_chat_styles the same way
        // ChatEvent validates a per-message style, so a forged packet can't
        // claim a bubble the player isn't permitted to use (e.g. VIP-only).
        if (chatBubbleId != 0 && (!_chatStyleManager.TryGetStyle(chatBubbleId, out var style)
            || style.RequiredRight.Length > 0 && !session.GetHabbo().Permissions.HasRight(style.RequiredRight)))
            return Task.CompletedTask;

        session.GetHabbo().CustomBubbleId = chatBubbleId;
        session.GetHabbo().SaveChatBubble(chatBubbleId.ToString());

        return Task.CompletedTask;
    }
}

using Plus.Communication.Packets.Outgoing.Moderation;
using Plus.Communication.Packets.Outgoing.Rooms.Avatar;
using Plus.Communication.Packets.Outgoing.Rooms.Chat;
using Plus.Core.Settings;
using Plus.HabboHotel.Corporations;
using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Moderation;
using Plus.HabboHotel.Quests;
using Plus.HabboHotel.Rooms.Chat.Commands;
using Plus.HabboHotel.Rooms.Chat.Filter;
using Plus.HabboHotel.Rooms.Chat.Logs;
using Plus.HabboHotel.Rooms.Chat.Styles;
using Plus.Utilities;

namespace Plus.Communication.Packets.Incoming.Rooms.Chat;

public class ShoutEvent : IPacketEvent
{
    private readonly IChatStyleManager _chatStyleManager;
    private readonly IChatlogManager _chatlogManager;
    private readonly IWordFilterManager _wordFilterManager;
    private readonly ICommandManager _commandManager;
    private readonly IModerationManager _moderationManager;
    private readonly ISettingsManager _settingsManager;
    private readonly IQuestManager _questManager;

    public ShoutEvent(
        IChatStyleManager chatStyleManager,
        IChatlogManager chatlogManager,
        IWordFilterManager wordFilterManager,
        ICommandManager commandManager,
        IModerationManager moderationManager,
        ISettingsManager settingsManager,
        IQuestManager questManager)
    {
        _chatStyleManager = chatStyleManager;
        _chatlogManager = chatlogManager;
        _wordFilterManager = wordFilterManager;
        _commandManager = commandManager;
        _moderationManager = moderationManager;
        _settingsManager = settingsManager;
        _questManager = questManager;
    }

    public async Task Parse(GameClient session, IIncomingPacket packet)
    {
        if (!session.GetHabbo().InRoom)
            return;
        var room = session.GetHabbo().CurrentRoom;
        if (room == null)
            return;
        var user = room.GetRoomUserManager().GetRoomUserByHabbo(session.GetHabbo().Id);
        if (user == null)
            return;
        var message = StringCharFilter.Escape(packet.ReadString());
        if (message.Length > 100)
            message = message.Substring(0, 100);
        var colour = packet.ReadInt();
        if (!_chatStyleManager.TryGetStyle(colour, out var style) ||
            style.RequiredRight.Length > 0 && !session.GetHabbo().Permissions.HasRight(style.RequiredRight))
            colour = 0;
        // pixelrp: the persisted bubble must still pass the style's required right
        // (e.g. a VIP bubble after VIP lapses) - fall back to the validated colour.
        var customBubble = session.GetHabbo().CustomBubbleId;
        if (customBubble != 0 && (!_chatStyleManager.TryGetStyle(customBubble, out var customStyle) || customStyle.RequiredRight.Length > 0 && !session.GetHabbo().Permissions.HasRight(customStyle.RequiredRight)))
            customBubble = 0;
        user.LastBubble = customBubble == 0 ? colour : customBubble;
        if (UnixTimestamp.GetNow() < session.GetHabbo().FloodTime && session.GetHabbo().FloodTime != 0)
            return;
        if (session.GetHabbo().TimeMuted > 0)
        {
            session.Send(new MutedComposer(session.GetHabbo().TimeMuted));
            return;
        }
        if (!session.GetHabbo().Permissions.HasRight("room_ignore_mute") && room.CheckMute(session))
        {
            session.SendWhisper("Oops, you're currently muted.");
            return;
        }
        if (!session.GetHabbo().Permissions.HasRight("mod_tool"))
        {
            if (user.IncrementAndCheckFlood(out var muteTime))
            {
                session.Send(new FloodControlComposer(muteTime));
                return;
            }
        }
        
        _chatlogManager.StoreChatlog(new(session.GetHabbo().Id, room.Id, message, UnixTimestamp.GetNow(), session.GetHabbo(), room));

        if (message.StartsWith(":", StringComparison.CurrentCulture) && await _commandManager.Parse(session, message))
            return;
        if (_wordFilterManager.CheckBannedWords(message))
        {
            session.GetHabbo().BannedPhraseCount++;
            if (session.GetHabbo().BannedPhraseCount >= Convert.ToInt32(_settingsManager.TryGetValue("room.chat.filter.banned_phrases.chances")))
            {
                _moderationManager.BanUser("System", ModerationBanType.Username, session.GetHabbo().Username, $"Spamming banned phrases ({message})",
                    UnixTimestamp.GetNow() + 78892200);
                session.Disconnect();
                return;
            }
            session.Send(new ShoutComposer(user.VirtualId, message, 0, colour));
            return;
        }
        if (!session.GetHabbo().Permissions.HasRight("word_filter_override"))
            message = _wordFilterManager.CheckMessage(message);
        _questManager.ProgressUserQuest(session, QuestType.SocialChat);
        user.UnIdle();
        user.OnChat(ShiftManager.ChatBubbleFor(session.GetHabbo(), user.LastBubble), message, true);
        // pixelrp: shouting "67" plays the six-seven gesture (the client maps
        // expression 67 to a built-in dance). Any enable is paused so the
        // gesture is visible; the room cycle reapplies it two ticks later.
        if (message.Trim() == "67")
        {
            if (user.DanceId > 0)
                user.DanceId = 0;
            if (session.GetHabbo().Effects.CurrentEffect > 0)
            {
                room.SendPacket(new AvatarEffectComposer(user.VirtualId, 0));
                user.EffectReapplyTimer = 2;
            }
            room.SendPacket(new ActionComposer(user.VirtualId, 67));
        }
        return;
    }
}
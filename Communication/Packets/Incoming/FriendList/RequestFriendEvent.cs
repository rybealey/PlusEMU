using Plus.HabboHotel.Friends;
using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Users.Accounts;
using Plus.HabboHotel.Quests;

namespace Plus.Communication.Packets.Incoming.FriendList;

internal class RequestFriendEvent : IPacketEvent
{
    private readonly IQuestManager _questManager;
    private readonly IMessengerDataLoader _messengerDataLoader;

    public RequestFriendEvent(IQuestManager questManager, IMessengerDataLoader messengerDataLoader)
    {
        _questManager = questManager;
        _messengerDataLoader = messengerDataLoader;
    }

    public async Task Parse(GameClient session, IIncomingPacket packet)
    {
        var (userId, blocked) = await _messengerDataLoader.CanReceiveFriendRequests(packet.ReadString());
        if (userId == 0 || blocked)
            return;

        // pixelrp: your own characters are not your contacts. Beyond the
        // oddity of it, "Contacts" is an audience in the profile privacy
        // screen - friending your own alt would be a way to show it the
        // things you told the hotel only contacts could see.
        if (AccountUtility.SameAccount(session.GetHabbo().Id, userId))
        {
            session.SendWhisper("That is one of your own characters.");
            return;
        }

        session.GetHabbo().Messenger.SendFriendRequest(userId);
        _questManager.ProgressUserQuest(session, QuestType.SocialFriend);
        return;
    }
}
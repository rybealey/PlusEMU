using Plus.Communication.Packets.Outgoing.FriendList;
using Plus.HabboHotel.Friends;
using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Users.Messenger;

namespace Plus.Communication.Packets.Incoming.FriendList;

internal class MessengerInitEvent : IPacketEvent
{
    private readonly IMessengerDataLoader _messengerDataLoader;
    private readonly IGameClientManager _gameClientManager;

    public MessengerInitEvent(IMessengerDataLoader messengerDataLoader, IGameClientManager gameClientManager)
    {
        _messengerDataLoader = messengerDataLoader;
        _gameClientManager = gameClientManager;
    }

    public async Task Parse(GameClient session, IIncomingPacket packet)
    {
        var friends = session.GetHabbo().Messenger.Friends.Values.ToList();
        session.Send(new MessengerInitComposer());
        var page = 0;
        if (!friends.Any())
            session.Send(new BuddyListComposer(friends, 1, 0));
        else
        {
            var pages = (friends.Count - 1) / 500 + 1;
            foreach (ICollection<MessengerBuddy> batch in friends.Chunk(500))
            {
                session.Send(new BuddyListComposer(batch.ToList(), pages, page));
                page++;
            }
        }

        var messages = await _messengerDataLoader.GetAndDeleteOfflineMessages(session.GetHabbo().Id);
        foreach (var (userId, report) in messages)
        {
            foreach (var (message, secondsAgo) in report)
                session.Send(new NewConsoleMessageComposer(userId, message, secondsAgo));
            // pixelrp: their stored messages just reached this client - tell
            // the sender's phone they were delivered, if they're online.
            var sender = _gameClientManager.GetClientByUserId(userId);
            if (sender?.GetHabbo()?.Messenger.GetFriend(session.GetHabbo().Id) != null)
                sender.Send(new RpMessengerReceiptComposer(session.GetHabbo().Id, RpMessengerReceiptComposer.Delivered));
        }
    }
}
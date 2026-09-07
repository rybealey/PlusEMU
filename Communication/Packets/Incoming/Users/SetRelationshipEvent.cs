using Plus.Communication.Packets.Outgoing.Moderation;
using Plus.Database;
using Plus.HabboHotel.Friends;
using Plus.HabboHotel.GameClients;

namespace Plus.Communication.Packets.Incoming.Users;

internal class SetRelationshipEvent : IPacketEvent
{
    private readonly IGameClientManager _clientManager;
    private readonly IDatabase _database;
    private readonly IMessengerDataLoader _messengerDataLoader;

    public SetRelationshipEvent(IGameClientManager clientManager, IDatabase database, IMessengerDataLoader messengerDataLoader)
    {
        _clientManager = clientManager;
        _database = database;
        _messengerDataLoader = messengerDataLoader;
    }

    public async Task Parse(GameClient session, IIncomingPacket packet)
    {
        var user = packet.ReadInt();
        var type = packet.ReadInt();
        var friend = session.GetHabbo().Messenger.GetFriend(user);
        if (friend == null)
        {
            session.SendNotification("Oops, you can only set a relationship where a friendship exists.");
            return;
        }
        if (type < 0 || type > 3)
        {
            session.SendNotification("Oops, you've chosen an invalid relationship type.");
            return;
        }

        friend.Relationship = type;
        await _messengerDataLoader.SetRelationship(session.GetHabbo().Id, friend.Id, friend.Relationship);
        session.GetHabbo().Messenger.UpdateFriend(friend);
        return;
    }
}
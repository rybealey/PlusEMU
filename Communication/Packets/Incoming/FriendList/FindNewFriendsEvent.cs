using Plus.Communication.Packets.Outgoing.FriendList;
using Plus.Communication.Packets.Outgoing.Rooms.Session;
using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Rooms;

namespace Plus.Communication.Packets.Incoming.FriendList;

internal class FindNewFriendsEvent : IPacketEvent
{
    private readonly IRoomManager _roomManager;

    public FindNewFriendsEvent(IRoomManager roomManager)
    {
        _roomManager = roomManager;
    }

    public Task Parse(GameClient session, IIncomingPacket packet)
    {
        // pixelrp: this teleports you to a random room, so it is travel too -
        // refused while cuffed, escorted, knocked out or jailed.
        var cannot = Plus.HabboHotel.Users.TravelGuard.Refusal(session);
        if (cannot != null)
        {
            session.Send(new FindFriendsProcessResultComposer(false));
            return Task.CompletedTask;
        }
        var instance = _roomManager.TryGetRandomLoadedRoom();
        if (instance != null)
        {
            session.Send(new FindFriendsProcessResultComposer(true));
            session.SendRoomForward(instance.Id);
        }
        else
            session.Send(new FindFriendsProcessResultComposer(false));
        return Task.CompletedTask;
    }
}
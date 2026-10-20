using Plus.Communication.Attributes;
using Plus.Communication.Packets.Outgoing.Rooms.Session;
using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Rooms;

namespace Plus.Communication.Packets.Incoming.Navigator;

[BuilderOnly]
internal class FindRandomFriendingRoomEvent : IPacketEvent
{
    private readonly IRoomManager _roomManager;

    public FindRandomFriendingRoomEvent(IRoomManager roomManager)
    {
        _roomManager = roomManager;
    }

    public Task Parse(GameClient session, IIncomingPacket packet)
    {
        // pixelrp: the forward below is the server's own, which the entry gates
        // wave through - so below the staff line this asks TravelGuard itself.
        if (!session.GetHabbo().IsStaff)
        {
            var cannot = Plus.HabboHotel.Users.TravelGuard.Refusal(session);
            if (cannot != null)
            {
                session.SendWhisper(cannot);
                return Task.CompletedTask;
            }
        }
        var instance = _roomManager.TryGetRandomLoadedRoom();
        if (instance != null)
            session.SendRoomForward(instance.Id);
        return Task.CompletedTask;
    }
}
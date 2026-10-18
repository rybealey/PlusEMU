using Plus.Communication.Packets.Outgoing.FriendList;
using Plus.Communication.Packets.Outgoing.Rooms.Session;
using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Rooms;

namespace Plus.Communication.Packets.Incoming.FriendList;

internal class FollowFriendEvent : IPacketEvent
{
    private readonly IGameClientManager _clientManager;

    public FollowFriendEvent(IGameClientManager clientManager)
    {
        _clientManager = clientManager;
    }

    public Task Parse(GameClient session, IIncomingPacket packet)
    {
        var buddyId = packet.ReadInt();
        if (buddyId == 0 || buddyId == session.GetHabbo().Id)
            return Task.CompletedTask;
        // pixelrp: you can only follow an actual friend. The id came off the
        // wire, so without this it would walk you to anyone online by id.
        if (!session.GetHabbo().Messenger.FriendshipExists(buddyId))
            return Task.CompletedTask;
        // pixelrp: and not while something is meant to be holding you in place -
        // following a friend was a way out of handcuffs and out of jail.
        var cannot = Plus.HabboHotel.Users.TravelGuard.Refusal(session);
        if (cannot != null)
        {
            session.SendWhisper(cannot);
            return Task.CompletedTask;
        }
        var client = _clientManager.GetClientByUserId(buddyId);
        if (client == null || client.GetHabbo() == null)
            return Task.CompletedTask;
        // Read once: the friend can change rooms between two reads.
        var room = client.GetHabbo().CurrentRoom;
        if (room == null)
        {
            session.Send(new FollowFriendFailedComposer(2));
            return Task.CompletedTask;
        }
        if (session.GetHabbo().CurrentRoom?.RoomId == room.RoomId)
            return Task.CompletedTask;
        // pixelrp: from inside a room, straight there, the way :summon moves
        // somebody, when nothing at the door can turn them away (RoomDoor).
        if (RoomDoor.LetsIn(session, room))
            session.GetHabbo().PrepareRoom(room.RoomId, "");
        else
            session.SendRoomForward(room.RoomId);
        return Task.CompletedTask;
    }
}
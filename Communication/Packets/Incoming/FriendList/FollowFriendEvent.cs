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
        // somebody - a forward makes the client ask for the room's details and
        // then ask to enter, two more round trips before the room change even
        // starts. Only when nothing at the door can turn them away, though:
        // PrepareRoom takes a player out of their room BEFORE its doorbell,
        // password, full and ban checks, and the forward lets the client deal
        // with those while they are still standing where they were.
        if (LetIn(session, room))
            session.GetHabbo().PrepareRoom(room.RoomId, "");
        else
            session.SendRoomForward(room.RoomId);
        return Task.CompletedTask;
    }

    /// <summary>
    /// PrepareRoom's own door checks, asked ahead of it. False for a room being
    /// unloaded (:unload, a floor plan save) - the forward, which only needs its
    /// id, copes with that as it always did.
    /// </summary>
    private static bool LetIn(GameClient session, Room room)
    {
        var habbo = session.GetHabbo();
        var users = room.GetRoomUserManager();
        var bans = room.GetBans();
        if (!habbo.InRoom || room.MDisposed || users == null || bans == null)
            return false;
        var permissions = habbo.Permissions;
        if ((room.Access == RoomAccess.Doorbell || room.Access == RoomAccess.Password)
            && !room.CheckRights(session, true, true) && !permissions.HasRight("room_enter_locked"))
            return false;
        if (users.UserCount >= room.UsersMax && !permissions.HasRight("room_enter_full") && habbo.Id != room.OwnerId)
            return false;
        return permissions.HasRight("room_ban_override") || !bans.IsBanned(habbo.Id);
    }
}
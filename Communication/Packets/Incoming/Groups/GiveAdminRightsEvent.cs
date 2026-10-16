using Plus.Communication.Packets.Outgoing.Groups;
using Plus.Communication.Packets.Outgoing.Rooms.Permissions;
using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Groups;
using Plus.HabboHotel.Rooms;

namespace Plus.Communication.Packets.Incoming.Groups;

internal class GiveAdminRightsEvent : IPacketEvent
{
    private readonly IGroupManager _groupManager;
    private readonly IRoomManager _roomManager;

    public GiveAdminRightsEvent(IGroupManager groupManager, IRoomManager roomManager)
    {
        _groupManager = groupManager;
        _roomManager = roomManager;
    }

    public Task Parse(GameClient session, IIncomingPacket packet)
    {
        var groupId = packet.ReadInt();
        var userId = packet.ReadInt();
        if (!_groupManager.TryGetGroup(groupId, out var group))
            return Task.CompletedTask;
        // pixelrp: a gang is a group flagged is_gang, but it is run only through the
        // gang window (the Rp* gang packets), which enforce roles, cost and turf. The
        // stock group packets skip all of that, so they do nothing to a gang.
        if (group.IsGang)
            return Task.CompletedTask;
        if (session.GetHabbo().Id != group.CreatorId || !group.IsMember(userId))
            return Task.CompletedTask;
        var habbo = PlusEnvironment.GetHabboById(userId);
        if (habbo == null)
        {
            session.SendNotification("Oops, an error occurred whilst finding this user.");
            return Task.CompletedTask;
        }
        group.MakeAdmin(userId);
        if (_roomManager.TryGetRoom(group.RoomId, out var room))
        {
            var user = room.GetRoomUserManager().GetRoomUserByHabbo(userId);
            if (user != null)
            {
                if (!user.Statusses.ContainsKey("flatctrl 3"))
                    user.SetStatus("flatctrl 3");
                user.UpdateNeeded = true;
                if (user.GetClient() != null)
                    user.GetClient().Send(new YouAreControllerComposer(3));
            }
        }
        session.Send(new GroupMemberUpdatedComposer(groupId, habbo, 1));
        return Task.CompletedTask;
    }
}
using Plus.Communication.Packets.Outgoing.Users;
using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Groups;

namespace Plus.Communication.Packets.Incoming.Users;

internal class GetHabboGroupBadgesEvent : IPacketEvent
{
    private readonly IGroupManager _groupManager;

    public GetHabboGroupBadgesEvent(IGroupManager groupManager) => _groupManager = groupManager;

    public Task Parse(GameClient session, IIncomingPacket packet)
    {
        if (!session.GetHabbo().InRoom)
            return Task.CompletedTask;
        var room = session.GetHabbo().CurrentRoom;
        if (room == null)
            return Task.CompletedTask;
        var badges = _groupManager.GetAllBadgesInRoom(room);
        if (badges == null)
            return Task.CompletedTask;
        // pixelrp: the asker (the client asks on every room entry) gets every
        // badge in the room, once - the whole list used to go to the whole room
        // and then to the asker a second time. Everyone else has the rest
        // already and only needs the asker's own favourite group, if any.
        session.Send(new HabboGroupBadgesComposer(badges));
        var groupId = session.GetHabbo().HabboStats.FavouriteGroupId;
        if (groupId > 0 && _groupManager.TryGetGroup(groupId, out var group))
        {
            var own = new HabboGroupBadgesComposer(group);
            foreach (var user in room.GetRoomUserManager().GetRoomUsers())
            {
                var client = user?.GetClient();
                if (client != null && client != session)
                    client.Send(own);
            }
        }
        return Task.CompletedTask;
    }
}
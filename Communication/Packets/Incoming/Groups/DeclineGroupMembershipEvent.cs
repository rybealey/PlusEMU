using Plus.Communication.Packets.Outgoing.Groups;
using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Groups;

namespace Plus.Communication.Packets.Incoming.Groups;

internal class DeclineGroupMembershipEvent : IPacketEvent
{
    private readonly IGroupManager _groupManager;

    public DeclineGroupMembershipEvent(IGroupManager groupManager)
    {
        _groupManager = groupManager;
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
        if (session.GetHabbo().Id != group.CreatorId && !group.IsAdmin(session.GetHabbo().Id))
            return Task.CompletedTask;
        if (!group.HasRequest(userId))
            return Task.CompletedTask;
        group.HandleRequest(userId, false);
        session.Send(new UnknownGroupComposer(group.Id, userId));
        return Task.CompletedTask;
    }
}
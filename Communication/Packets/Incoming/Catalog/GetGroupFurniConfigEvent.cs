using Plus.Communication.Attributes;
using Plus.Communication.Packets.Outgoing.Catalog;
using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Groups;

namespace Plus.Communication.Packets.Incoming.Catalog;

[StaffOnly]
internal class GetGroupFurniConfigEvent : IPacketEvent
{
    private readonly IGroupManager _groupManager;

    public GetGroupFurniConfigEvent(IGroupManager groupManager)
    {
        _groupManager = groupManager;
    }

    public Task Parse(GameClient session, IIncomingPacket packet)
    {
        session.Send(new GroupFurniConfigComposer(_groupManager.GetGroupsForUser(session.GetHabbo().Id)));
        return Task.CompletedTask;
    }
}
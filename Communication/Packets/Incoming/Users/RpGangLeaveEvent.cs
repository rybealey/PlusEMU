using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Gangs;
using Plus.HabboHotel.Groups;

namespace Plus.Communication.Packets.Incoming.Users;

/// <summary>
/// pixelrp: leave the gang. For the leader this DISBANDS it (the client
/// confirms with that wording): members are freed and told, invites void.
/// </summary>
internal class RpGangLeaveEvent : IPacketEvent
{
    private readonly IGroupManager _groupManager;

    public RpGangLeaveEvent(IGroupManager groupManager)
    {
        _groupManager = groupManager;
    }

    public Task Parse(GameClient session, IIncomingPacket packet)
    {
        var actor = GangManager.GetActor(session, 0);
        if (actor == null)
            return Task.CompletedTask;

        var name = actor.Snapshot.Gang.Name;
        if (actor.IsLeader)
        {
            var others = actor.Snapshot.Members.Where(member => member.UserId != actor.UserId).Select(member => member.UserId).ToList();
            GangManager.Disband(_groupManager, actor.GangId);
            session.SendWhisper($"{name} has been disbanded.");
            foreach (var userId in others)
                GangManager.Alert(userId, $"{name} was disbanded by its leader.");
            return Task.CompletedTask;
        }

        GangManager.RemoveMember(_groupManager, actor.GangId, actor.UserId);
        session.SendWhisper($"You left {name}.");
        GangManager.BroadcastDetail(actor.GangId);
        return Task.CompletedTask;
    }
}

using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Gangs;
using Plus.HabboHotel.Groups;

namespace Plus.Communication.Packets.Incoming.Users;

/// <summary>
/// pixelrp: remove a member (Manage tab). Requires the kick permission; the
/// leader can't be kicked and administrators only by the leader.
/// </summary>
internal class RpGangKickEvent : IPacketEvent
{
    private readonly IGroupManager _groupManager;

    public RpGangKickEvent(IGroupManager groupManager)
    {
        _groupManager = groupManager;
    }

    public Task Parse(GameClient session, IIncomingPacket packet)
    {
        var userId = packet.ReadInt();
        var actor = GangManager.GetActor(session, GangManager.PermKick);
        if (actor == null)
            return Task.CompletedTask;

        var target = actor.Snapshot.Members.FirstOrDefault(member => member.UserId == userId);
        if (target == null)
        {
            session.SendWhisper("That player isn't in your gang.");
            return Task.CompletedTask;
        }
        if (userId == actor.UserId)
        {
            session.SendWhisper("Use Leave Gang to leave.");
            return Task.CompletedTask;
        }
        if (userId == actor.Snapshot.Gang.OwnerId)
        {
            session.SendWhisper("You can't kick the leader.");
            return Task.CompletedTask;
        }
        if (!actor.IsLeader && (GangManager.PermissionsOf(actor.Snapshot, userId) & GangManager.PermAdmin) != 0)
        {
            session.SendWhisper("Only the leader can kick an administrator.");
            return Task.CompletedTask;
        }

        var name = actor.Snapshot.Gang.Name;
        GangManager.RemoveMember(_groupManager, actor.GangId, userId);
        session.SendWhisper($"{target.Username} was kicked from {name}.");
        GangManager.Alert(userId, $"You were kicked from {name}.");
        GangManager.BroadcastDetail(actor.GangId);
        return Task.CompletedTask;
    }
}

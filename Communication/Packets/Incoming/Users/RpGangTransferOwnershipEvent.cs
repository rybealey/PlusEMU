using Dapper;
using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Gangs;
using Plus.HabboHotel.Groups;

namespace Plus.Communication.Packets.Incoming.Users;

/// <summary>
/// pixelrp: hand the gang to another member from the Manage tab. Only the
/// owner can do this. Ownership is groups.owner_id, not a role, so nobody's
/// rank changes: the old owner stays in the gang, in whatever role they held,
/// and loses only the owner's rights (disband, transfer, kick-proof).
/// </summary>
internal class RpGangTransferOwnershipEvent : IPacketEvent
{
    private readonly IGroupManager _groupManager;

    public RpGangTransferOwnershipEvent(IGroupManager groupManager)
    {
        _groupManager = groupManager;
    }

    public Task Parse(GameClient session, IIncomingPacket packet)
    {
        var userId = packet.ReadInt();
        var actor = GangManager.GetActor(session, GangManager.PermOwner);
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
            session.SendWhisper("You already own this gang.");
            return Task.CompletedTask;
        }

        using (var connection = PlusEnvironment.DatabaseManager.Connection())
        {
            connection.Execute("UPDATE `groups` SET `owner_id` = @userId WHERE `id` = @gangId AND `is_gang` = '1'", new { userId, gangId = actor.GangId });
        }
        if (_groupManager.TryGetGroup(actor.GangId, out var group))
            group.CreatorId = userId;

        var name = actor.Snapshot.Gang.Name;
        session.SendWhisper($"{target.Username} now owns {name}.");
        GangManager.Alert(userId, $"You are now the owner of {name}.");
        GangManager.BroadcastDetail(actor.GangId);
        GangManager.BroadcastMembershipOfAll(actor.GangId);
        return Task.CompletedTask;
    }
}

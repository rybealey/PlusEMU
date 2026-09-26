using Dapper;
using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Gangs;
using Plus.HabboHotel.Groups;

namespace Plus.Communication.Packets.Incoming.Users;

/// <summary>
/// pixelrp: change the gang's two colours from its Settings tab - RAW RGB
/// ints, the same shape founding sends (gangs bypass the groups_items colour
/// ids). Requires Administrator, which the owner always holds. Free.
/// </summary>
internal class RpGangSetColoursEvent : IPacketEvent
{
    private readonly IGroupManager _groupManager;

    public RpGangSetColoursEvent(IGroupManager groupManager)
    {
        _groupManager = groupManager;
    }

    public Task Parse(GameClient session, IIncomingPacket packet)
    {
        var colourA = packet.ReadInt() & 0xFFFFFF;
        var colourB = packet.ReadInt() & 0xFFFFFF;
        var actor = GangManager.GetActor(session, GangManager.PermAdmin);
        if (actor == null)
            return Task.CompletedTask;

        using (var connection = PlusEnvironment.DatabaseManager.Connection())
        {
            connection.Execute("UPDATE `groups` SET `colour1` = @colourA, `colour2` = @colourB WHERE `id` = @gangId AND `is_gang` = '1'",
                new { colourA, colourB, gangId = actor.GangId });
        }
        if (_groupManager.TryGetGroup(actor.GangId, out var group))
        {
            group.Colour1 = colourA;
            group.Colour2 = colourB;
        }

        session.SendWhisper("Your gang's colours were saved.");
        GangManager.BroadcastDetail(actor.GangId);
        GangManager.BroadcastMembershipOfAll(actor.GangId);
        return Task.CompletedTask;
    }
}

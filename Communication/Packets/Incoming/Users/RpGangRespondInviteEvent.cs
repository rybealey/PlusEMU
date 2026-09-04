using Dapper;
using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Gangs;
using Plus.HabboHotel.Groups;

namespace Plus.Communication.Packets.Incoming.Users;

/// <summary>
/// pixelrp: accept (1) or decline (0) a gang invite from the create view's
/// invite banner. Accepting joins the group and voids every other invite the
/// player held; both outcomes refresh the responder's window state.
/// </summary>
internal class RpGangRespondInviteEvent : IPacketEvent
{
    private readonly IGroupManager _groupManager;

    public RpGangRespondInviteEvent(IGroupManager groupManager)
    {
        _groupManager = groupManager;
    }

    public Task Parse(GameClient session, IIncomingPacket packet)
    {
        var gangId = packet.ReadInt();
        var accept = packet.ReadInt() == 1;
        var habbo = session.GetHabbo();
        if (habbo == null || gangId <= 0)
            return Task.CompletedTask;

        if (GangUtility.GetGang(habbo.Id) != null)
        {
            session.SendWhisper("You're already in a gang.");
            GangManager.SendState(session);
            return Task.CompletedTask;
        }

        int? inviteId;
        using (var connection = PlusEnvironment.DatabaseManager.Connection())
        {
            inviteId = connection.QueryFirstOrDefault<int?>(
                "SELECT `id` FROM `rp_gang_invites` WHERE `gang_id` = @gangId AND `user_id` = @userId AND `expires_at` > @now LIMIT 1",
                new { gangId, userId = habbo.Id, now = GangManager.Now() });
        }
        if (inviteId == null)
        {
            session.SendWhisper("That invite has expired.");
            GangManager.SendState(session);
            return Task.CompletedTask;
        }

        var gang = GangManager.GetGang(gangId);
        if (!accept || gang == null)
        {
            using (var connection = PlusEnvironment.DatabaseManager.Connection())
            {
                connection.Execute("DELETE FROM `rp_gang_invites` WHERE `id` = @id", new { id = inviteId.Value });
            }
            GangManager.SendState(session);
            if (gang != null)
                GangManager.BroadcastDetail(gangId);
            return Task.CompletedTask;
        }

        GangManager.AddMember(_groupManager, gangId, habbo.Id);
        session.SendWhisper($"You joined {gang.Name}.");
        GangManager.BroadcastDetail(gangId);
        return Task.CompletedTask;
    }
}

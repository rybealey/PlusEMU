using Dapper;
using Plus.HabboHotel.Corporations;
using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Gangs;

namespace Plus.Communication.Packets.Incoming.Users;

/// <summary>
/// pixelrp: invite a player by name to the actor's gang (Invites tab).
/// Requires the invite permission. Re-inviting refreshes the expiry.
/// </summary>
internal class RpGangInviteEvent : IPacketEvent
{
    public Task Parse(GameClient session, IIncomingPacket packet)
    {
        var username = packet.ReadString().Trim();
        var actor = GangManager.GetActor(session, GangManager.PermInvite);
        if (actor == null || string.IsNullOrWhiteSpace(username))
            return Task.CompletedTask;

        var target = CorporationUtility.ResolveUser(username);
        if (target == null)
        {
            session.SendWhisper($"No player named '{username}' exists.");
            return Task.CompletedTask;
        }
        if (target.Id == actor.UserId || actor.Snapshot.Members.Any(member => member.UserId == target.Id))
        {
            session.SendWhisper($"{target.Username} is already in your gang.");
            return Task.CompletedTask;
        }
        if (GangUtility.GetGang(target.Id) != null)
        {
            session.SendWhisper($"{target.Username} is already in a gang.");
            return Task.CompletedTask;
        }
        var alreadyInvited = actor.Snapshot.Invites.Any(invite => invite.UserId == target.Id);
        if (!alreadyInvited && actor.Snapshot.Invites.Count >= GangManager.MaxPendingInvites)
        {
            session.SendWhisper($"Your gang already has {GangManager.MaxPendingInvites} pending invites - cancel some first.");
            return Task.CompletedTask;
        }

        using (var connection = PlusEnvironment.DatabaseManager.Connection())
        {
            connection.Execute(
                "INSERT INTO `rp_gang_invites` (`gang_id`, `user_id`, `invited_by`, `expires_at`) VALUES (@gangId, @userId, @invitedBy, @expiresAt) " +
                "ON DUPLICATE KEY UPDATE `invited_by` = VALUES(`invited_by`), `expires_at` = VALUES(`expires_at`)",
                new { gangId = actor.GangId, userId = target.Id, invitedBy = actor.UserId, expiresAt = GangManager.Now() + GangManager.InviteHours() * 3600 });
        }

        session.SendWhisper(alreadyInvited ? $"Invite to {target.Username} renewed." : $"Invite sent to {target.Username}.");
        GangManager.BroadcastDetail(actor.GangId);
        GangManager.SendIncomingInvites(target.Id);
        if (target.Client?.GetHabbo() != null)
            target.Client.SendWhisper($"{session.GetHabbo().Username} invited you to join {actor.Snapshot.Gang.Name} - open the Gang window to respond.");
        return Task.CompletedTask;
    }
}

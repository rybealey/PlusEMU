using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Sitch;

namespace Plus.Communication.Packets.Incoming.Users.Sitch;

/// <summary>
/// pixelrp: remove a Sitch post. Your own always; anyone's with
/// rp_sitch_moderate, which is where this hotel keeps UI permissions
/// (103_FurniFunction did the same for the Function tool).
///
/// Soft delete - the row stays, carrying who removed it - so a staff removal
/// can be read back afterwards.
/// </summary>
internal class RpSitchDeleteEvent : IPacketEvent
{
    public Task Parse(GameClient session, IIncomingPacket packet)
    {
        var postId = packet.ReadInt();
        var habbo = session.GetHabbo();
        if (habbo == null || postId <= 0) return Task.CompletedTask;

        var staff = habbo.Permissions.HasCommand("rp_sitch_moderate");
        if (!SitchUtility.DeletePost(postId, habbo.Id, staff))
        {
            session.SendWhisper("That post is not yours to remove.");
            return Task.CompletedTask;
        }

        SitchUtility.SendFeed(session, false);
        return Task.CompletedTask;
    }
}

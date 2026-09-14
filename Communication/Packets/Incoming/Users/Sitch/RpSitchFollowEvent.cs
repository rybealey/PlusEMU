using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Sitch;

namespace Plus.Communication.Packets.Incoming.Users.Sitch;

/// <summary>pixelrp: follow or unfollow somebody. 1 = follow.</summary>
internal class RpSitchFollowEvent : IPacketEvent
{
    public Task Parse(GameClient session, IIncomingPacket packet)
    {
        var userId = packet.ReadInt();
        var on = packet.ReadInt() == 1;
        var habbo = session.GetHabbo();
        if (habbo == null || userId <= 0) return Task.CompletedTask;

        SitchUtility.SetFollow(habbo.Id, userId, on);
        SitchUtility.SendProfile(session, userId);
        return Task.CompletedTask;
    }
}

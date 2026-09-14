using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Sitch;

namespace Plus.Communication.Packets.Incoming.Users.Sitch;

/// <summary>
/// pixelrp: a Sitch profile opened. 0 means "mine", so the client need not know
/// its own id.
///
/// A trailing username is optional and only read when the id is 0: that is how
/// tapping an @mention opens a profile, since the post body carries the name
/// and not the id. An unknown name falls back to your own profile rather than
/// to an empty screen.
/// </summary>
internal class RpGetSitchProfileEvent : IPacketEvent
{
    public Task Parse(GameClient session, IIncomingPacket packet)
    {
        var habbo = session.GetHabbo();
        if (habbo == null) return Task.CompletedTask;
        var userId = packet.HasDataRemaining() ? packet.ReadInt() : 0;

        if (userId <= 0 && packet.HasDataRemaining())
        {
            var username = packet.ReadString();
            if (!string.IsNullOrWhiteSpace(username)) userId = SitchUtility.ResolveUsername(username);
        }

        SitchUtility.SendProfile(session, (userId > 0) ? userId : habbo.Id);
        return Task.CompletedTask;
    }
}

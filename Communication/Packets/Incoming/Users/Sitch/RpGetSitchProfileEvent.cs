using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Sitch;

namespace Plus.Communication.Packets.Incoming.Users.Sitch;

/// <summary>pixelrp: a Sitch profile opened. 0 means "mine", so the client need not know its own id.</summary>
internal class RpGetSitchProfileEvent : IPacketEvent
{
    public Task Parse(GameClient session, IIncomingPacket packet)
    {
        var habbo = session.GetHabbo();
        if (habbo == null) return Task.CompletedTask;
        var userId = packet.HasDataRemaining() ? packet.ReadInt() : 0;
        SitchUtility.SendProfile(session, (userId > 0) ? userId : habbo.Id);
        return Task.CompletedTask;
    }
}

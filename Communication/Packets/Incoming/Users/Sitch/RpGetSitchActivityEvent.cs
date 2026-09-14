using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Sitch;

namespace Plus.Communication.Packets.Incoming.Users.Sitch;

/// <summary>pixelrp: the Sitch Activity tab opened.</summary>
internal class RpGetSitchActivityEvent : IPacketEvent
{
    public Task Parse(GameClient session, IIncomingPacket packet)
    {
        if (session.GetHabbo() == null) return Task.CompletedTask;
        SitchUtility.SendActivity(session);
        return Task.CompletedTask;
    }
}

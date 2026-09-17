using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Jam;

namespace Plus.Communication.Packets.Incoming.Jam;

// PixelRP: skip the jam's current song. ANY member may - see JamSession.TrySkip
// for why that is deliberate next to a pause only the host has.
internal class RpJamSkipEvent : IPacketEvent
{
    public Task Parse(GameClient session, IIncomingPacket packet)
    {
        var habbo = session.GetHabbo();
        if (habbo == null)
            return Task.CompletedTask;
        JamManager.GetFor(habbo.Id)?.TrySkip(session);
        return Task.CompletedTask;
    }
}

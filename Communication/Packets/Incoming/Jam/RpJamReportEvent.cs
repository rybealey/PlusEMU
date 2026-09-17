using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Jam;

namespace Plus.Communication.Packets.Incoming.Jam;

// PixelRP: a member's player reporting the real duration, or that the track has
// finished. Same contract as the room jukebox's report, with one difference: a
// jam has several players watching one track, so the first credible duration
// wins and later ones change nothing.
internal class RpJamReportEvent : IPacketEvent
{
    public Task Parse(GameClient session, IIncomingPacket packet)
    {
        var durationSec = packet.ReadInt();
        var ended = packet.ReadBool();
        var habbo = session.GetHabbo();
        if (habbo == null)
            return Task.CompletedTask;
        JamManager.GetFor(habbo.Id)?.Report(session, durationSec, ended);
        return Task.CompletedTask;
    }
}

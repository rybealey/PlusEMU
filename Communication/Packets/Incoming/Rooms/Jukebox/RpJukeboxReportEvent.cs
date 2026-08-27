using Plus.HabboHotel.GameClients;

namespace Plus.Communication.Packets.Incoming.Rooms.Jukebox;

// PixelRP: client reports the real player duration once loaded, and whether
// the track ended. Wire contract for `ended` is int 0/1 (not a wire bool) to
// match the Task 4 client composer, so we read it with ReadInt() == 1.
internal class RpJukeboxReportEvent : IPacketEvent
{
    public Task Parse(GameClient session, IIncomingPacket packet)
    {
        var durationSec = packet.ReadInt();
        var ended = packet.ReadInt() == 1;
        session.GetHabbo()?.CurrentRoom?.GetJukeboxManager().Report(session, durationSec, ended);
        return Task.CompletedTask;
    }
}

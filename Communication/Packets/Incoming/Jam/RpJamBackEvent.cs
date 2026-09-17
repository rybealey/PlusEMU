using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Jam;

namespace Plus.Communication.Packets.Incoming.Jam;

// PixelRP: the back button.
//
// One packet for two jobs, because from the player's side it is one button.
// Past the first few seconds of a track it restarts it; inside them it steps
// back to the song before. JamSession decides which, since only the server
// knows how far in the jam actually is.
//
// Any member may, like skip.
internal class RpJamBackEvent : IPacketEvent
{
    public Task Parse(GameClient session, IIncomingPacket packet)
    {
        var habbo = session.GetHabbo();
        if (habbo == null)
            return Task.CompletedTask;
        JamManager.GetFor(habbo.Id)?.TryBack(session);
        return Task.CompletedTask;
    }
}

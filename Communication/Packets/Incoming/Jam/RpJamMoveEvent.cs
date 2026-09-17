using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Jam;

namespace Plus.Communication.Packets.Incoming.Jam;

// PixelRP: the host drags a song up or down the jam's queue.
//
// Both ends come from the client, and both are checked against the queue as it
// is NOW rather than as the client was showing it - a song can be removed, or
// start playing, while a finger is down. JamSession.TryMove does the checking.
internal class RpJamMoveEvent : IPacketEvent
{
    public Task Parse(GameClient session, IIncomingPacket packet)
    {
        var from = packet.ReadInt();
        var to = packet.ReadInt();
        var habbo = session.GetHabbo();
        if (habbo == null)
            return Task.CompletedTask;
        JamManager.GetFor(habbo.Id)?.TryMove(session, from, to);
        return Task.CompletedTask;
    }
}

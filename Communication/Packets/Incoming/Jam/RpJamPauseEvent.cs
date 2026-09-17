using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Jam;

namespace Plus.Communication.Packets.Incoming.Jam;

// PixelRP: the host's pause, which is everyone's.
//
// A guest's pause never gets here - it stops their own player and leaves the
// jam running for the others, which is the only pause a guest is given. Silence
// you can impose on five people is not the same button as silence you impose on
// yourself, so they are not the same packet either.
internal class RpJamPauseEvent : IPacketEvent
{
    public Task Parse(GameClient session, IIncomingPacket packet)
    {
        var paused = packet.ReadBool();
        var habbo = session.GetHabbo();
        if (habbo == null)
            return Task.CompletedTask;
        JamManager.GetFor(habbo.Id)?.TrySetPaused(session, paused);
        return Task.CompletedTask;
    }
}

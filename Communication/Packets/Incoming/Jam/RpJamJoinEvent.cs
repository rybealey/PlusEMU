using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Jam;

namespace Plus.Communication.Packets.Incoming.Jam;

// PixelRP: accept an invite. The jam id comes from the invite card in the
// player's messages, so a stale card names a jam that has since ended - which
// is a "that jam has finished", not an error.
internal class RpJamJoinEvent : IPacketEvent
{
    public Task Parse(GameClient session, IIncomingPacket packet)
    {
        var jamId = packet.ReadInt();
        var habbo = session.GetHabbo();
        if (habbo == null)
            return Task.CompletedTask;
        var jam = JamManager.Join(habbo, jamId);
        if (jam == null)
            session.SendNotification("That jam has finished.");
        // Either way. A failed join has to land as "you are in nothing" rather
        // than silence, and a join that only woke an existing membership does
        // not broadcast, so this is the one path that always answers.
        JamManager.SendState(session);
        return Task.CompletedTask;
    }
}

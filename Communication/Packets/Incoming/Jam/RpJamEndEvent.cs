using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Jam;

namespace Plus.Communication.Packets.Incoming.Jam;

// PixelRP: the host ends the jam, for everybody.
//
// NOT the same button as leaving, which is why it is not the same packet. A
// host who leaves hands the jam to whoever has been in it longest and it carries
// on without them; a host who ends it stops it, and everyone goes back to their
// own ears. Both are things a host might reasonably want, so both exist.
//
// The host's alone. A guest who wants out leaves.
internal class RpJamEndEvent : IPacketEvent
{
    public Task Parse(GameClient session, IIncomingPacket packet)
    {
        JamManager.End(session.GetHabbo());
        return Task.CompletedTask;
    }
}

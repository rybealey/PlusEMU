using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Rooms;

namespace Plus.Communication.Packets.Incoming.Rooms.Apartments;

/// <summary>
/// pixelrp: one action from the Apartments window - refresh (0), buy (1: the
/// lot picked - the apartment itself, or one more lot), go to my apartment
/// (2), sell back (3), list for sale (4: the asking price) or take off sale
/// (5). One int follows the action whatever it is, 0 when unused.
/// RpApartments answers with the window again.
/// </summary>
internal class RpApartmentActionEvent : IPacketEvent
{
    public Task Parse(GameClient session, IIncomingPacket packet)
    {
        // Two ints or it is not the window's: ignored, never read past its end.
        if (session?.GetHabbo() == null || packet.Buffer.Length < 2 * sizeof(int))
            return Task.CompletedTask;

        var action = packet.ReadInt();
        var value = packet.ReadInt();
        RpApartments.Handle(session, action, value);
        return Task.CompletedTask;
    }
}

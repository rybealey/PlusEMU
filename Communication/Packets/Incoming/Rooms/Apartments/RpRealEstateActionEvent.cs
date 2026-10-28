using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Rooms;

namespace Plus.Communication.Packets.Incoming.Rooms.Apartments;

/// <summary>
/// pixelrp: one action from the real estate board - refresh (0), buy (1) or
/// visit (2), each with the listed apartment's room id and the asking price
/// the board showed (a buy at any other price is refused). 0s when unused.
/// RpRealEstate answers with the board again.
/// </summary>
internal class RpRealEstateActionEvent : IPacketEvent
{
    public Task Parse(GameClient session, IIncomingPacket packet)
    {
        // Three ints or it is not the board's: ignored, never read past its end.
        if (session?.GetHabbo() == null || packet.Buffer.Length < 3 * sizeof(int))
            return Task.CompletedTask;

        var action = packet.ReadInt();
        var roomId = packet.ReadInt();
        var price = packet.ReadInt();
        RpRealEstate.Handle(session, action, roomId, price);
        return Task.CompletedTask;
    }
}

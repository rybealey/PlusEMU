using Plus.Communication.Packets.Outgoing.CityPanel;
using Plus.HabboHotel.CityPanel;
using Plus.HabboHotel.GameClients;

namespace Plus.Communication.Packets.Incoming.CityPanel;

/// <summary>
/// pixelrp City Panel: a page of the Economy tab's Trade History - a player
/// search (a username prefix, or empty) and the page's offset. Needs the
/// Economy permission, like the Global Ledger.
/// </summary>
internal class RpCityTradesEvent : IPacketEvent
{
    public Task Parse(GameClient session, IIncomingPacket packet)
    {
        var query = packet.ReadString();
        var offset = packet.ReadInt();
        if (!CityPanelAccess.Can(session.GetHabbo(), CityPanelAccess.Capability.Economy))
            return Task.CompletedTask;
        if (query.Length > 32)
            query = query[..32];
        var (rows, more) = CityTrades.Page(query, offset);
        session.Send(new RpCityTradesComposer(offset, more, rows));
        return Task.CompletedTask;
    }
}

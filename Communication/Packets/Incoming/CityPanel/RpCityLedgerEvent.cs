using Plus.Communication.Packets.Outgoing.CityPanel;
using Plus.HabboHotel.CityPanel;
using Plus.HabboHotel.GameClients;

namespace Plus.Communication.Packets.Incoming.CityPanel;

/// <summary>
/// pixelrp City Panel: a page of the Economy tab's Global Ledger - a player
/// search (a username prefix, or empty for everybody), a filter
/// (CityLedger.Filter*) and the offset of the page. Every player's money is in
/// it, so it needs the Economy permission rather than only the panel's.
/// </summary>
internal class RpCityLedgerEvent : IPacketEvent
{
    public Task Parse(GameClient session, IIncomingPacket packet)
    {
        var query = packet.ReadString();
        var filter = packet.ReadInt();
        var offset = packet.ReadInt();
        if (!CityPanelAccess.Can(session.GetHabbo(), CityPanelAccess.Capability.Economy))
            return Task.CompletedTask;
        if (query.Length > 32)
            query = query[..32];
        var (rows, more) = CityLedger.Page(query, filter, offset);
        session.Send(new RpCityLedgerComposer(offset, more, CityLedger.CityTotals(), rows));
        return Task.CompletedTask;
    }
}

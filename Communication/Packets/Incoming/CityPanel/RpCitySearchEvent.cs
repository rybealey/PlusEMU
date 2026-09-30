using Plus.Communication.Packets.Outgoing.CityPanel;
using Plus.HabboHotel.CityPanel;
using Plus.HabboHotel.GameClients;

namespace Plus.Communication.Packets.Incoming.CityPanel;

/// <summary>pixelrp City Panel: the Players tab's search - a name prefix and a filter (CityPlayers.Filter*).</summary>
internal class RpCitySearchEvent : IPacketEvent
{
    public Task Parse(GameClient session, IIncomingPacket packet)
    {
        var query = packet.ReadString() ?? "";
        var filter = packet.ReadInt();
        if (!CityPanelAccess.CanOpen(session.GetHabbo()))
            return Task.CompletedTask;
        session.Send(new RpCitySearchComposer(CityPlayers.Search(query, filter)));
        return Task.CompletedTask;
    }
}

using Plus.Communication.Packets.Outgoing.CityPanel;
using Plus.HabboHotel.CityPanel;
using Plus.HabboHotel.GameClients;

namespace Plus.Communication.Packets.Incoming.CityPanel;

/// <summary>pixelrp City Panel: the Economy tab's state.</summary>
internal class RpCityEconomyEvent : IPacketEvent
{
    public Task Parse(GameClient session, IIncomingPacket packet)
    {
        if (!CityPanelAccess.CanOpen(session.GetHabbo()))
            return Task.CompletedTask;
        session.Send(new RpCityEconomyComposer(CityEconomy.Corporations(), ServicePrices.All()));
        return Task.CompletedTask;
    }
}

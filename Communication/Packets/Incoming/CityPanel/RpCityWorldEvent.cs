using Plus.Communication.Packets.Outgoing.CityPanel;
using Plus.HabboHotel.CityPanel;
using Plus.HabboHotel.GameClients;

namespace Plus.Communication.Packets.Incoming.CityPanel;

/// <summary>pixelrp City Panel: the City tab's state.</summary>
internal class RpCityWorldEvent : IPacketEvent
{
    public Task Parse(GameClient session, IIncomingPacket packet)
    {
        if (!CityPanelAccess.CanOpen(session.GetHabbo()))
            return Task.CompletedTask;
        session.Send(new RpCityWorldComposer(CityWorld.Current()));
        return Task.CompletedTask;
    }
}

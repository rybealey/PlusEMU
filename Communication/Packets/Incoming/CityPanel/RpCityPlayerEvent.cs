using Plus.Communication.Packets.Outgoing.CityPanel;
using Plus.HabboHotel.CityPanel;
using Plus.HabboHotel.GameClients;

namespace Plus.Communication.Packets.Incoming.CityPanel;

/// <summary>pixelrp City Panel: open one player's card.</summary>
internal class RpCityPlayerEvent : IPacketEvent
{
    public Task Parse(GameClient session, IIncomingPacket packet)
    {
        var userId = packet.ReadInt();
        if (!CityPanelAccess.CanOpen(session.GetHabbo()))
            return Task.CompletedTask;
        var card = CityPlayers.Card(userId);
        if (card != null)
            session.Send(new RpCityPlayerComposer(card));
        return Task.CompletedTask;
    }
}

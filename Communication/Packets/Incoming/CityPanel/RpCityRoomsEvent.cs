using Plus.Communication.Packets.Outgoing.CityPanel;
using Plus.HabboHotel.CityPanel;
using Plus.HabboHotel.GameClients;

namespace Plus.Communication.Packets.Incoming.CityPanel;

/// <summary>pixelrp City Panel: the Rooms &amp; Zones list - a name or id, and a filter (CityRooms.Filter*).</summary>
internal class RpCityRoomsEvent : IPacketEvent
{
    public Task Parse(GameClient session, IIncomingPacket packet)
    {
        var query = packet.ReadString() ?? "";
        var filter = packet.ReadInt();
        if (!CityPanelAccess.CanOpen(session.GetHabbo()))
            return Task.CompletedTask;
        session.Send(new RpCityRoomsComposer(CityRooms.List(query, filter)));
        return Task.CompletedTask;
    }
}

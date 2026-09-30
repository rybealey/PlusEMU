using Plus.HabboHotel.CityPanel;
using Plus.HabboHotel.GameClients;

namespace Plus.Communication.Packets.Outgoing.CityPanel;

/// <summary>pixelrp City Panel: the Rooms &amp; Zones list.</summary>
public class RpCityRoomsComposer : IServerPacket
{
    private readonly List<CityRoomRow> _rows;

    public uint MessageId => ServerPacketHeader.RpCityRoomsComposer;

    public RpCityRoomsComposer(List<CityRoomRow> rows) => _rows = rows;

    public void Compose(IOutgoingPacket packet)
    {
        packet.WriteInteger(_rows.Count);
        foreach (var row in _rows)
        {
            packet.WriteInteger(row.Id);
            packet.WriteString(row.Name);
            packet.WriteInteger(row.Zone);
            packet.WriteString(row.TurfHolder);
            packet.WriteBoolean(row.JailRoom);
            packet.WriteInteger(row.ArrestPoints);
            packet.WriteInteger(row.UsersNow);
            packet.WriteInteger(row.UsersMax);
        }
    }
}

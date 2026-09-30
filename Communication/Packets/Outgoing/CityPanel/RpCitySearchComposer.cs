using Plus.HabboHotel.CityPanel;
using Plus.HabboHotel.GameClients;

namespace Plus.Communication.Packets.Outgoing.CityPanel;

/// <summary>pixelrp City Panel: the Players tab's search results.</summary>
public class RpCitySearchComposer : IServerPacket
{
    private readonly List<CityPlayerRow> _rows;

    public uint MessageId => ServerPacketHeader.RpCitySearchComposer;

    public RpCitySearchComposer(List<CityPlayerRow> rows) => _rows = rows;

    public void Compose(IOutgoingPacket packet)
    {
        packet.WriteInteger(_rows.Count);
        foreach (var row in _rows)
        {
            packet.WriteInteger(row.Id);
            packet.WriteString(row.Username);
            packet.WriteString(row.Look);
            packet.WriteString(row.Gender);
            packet.WriteBoolean(row.Online);
            packet.WriteString(row.Where);
            packet.WriteInteger((int)row.Flags);
        }
    }
}

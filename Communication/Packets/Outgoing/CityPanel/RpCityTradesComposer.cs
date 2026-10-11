using Plus.HabboHotel.CityPanel;
using Plus.HabboHotel.GameClients;

namespace Plus.Communication.Packets.Outgoing.CityPanel;

/// <summary>
/// pixelrp City Panel: a page of Trade History - the offset it starts at,
/// whether another follows, then the trades, newest first. What each side
/// gave is the stored "item:count,..." string; the client names the items.
/// </summary>
public class RpCityTradesComposer : IServerPacket
{
    private readonly int _offset;
    private readonly bool _more;
    private readonly List<CityTrades.Row> _rows;

    public uint MessageId => ServerPacketHeader.RpCityTradesComposer;

    public RpCityTradesComposer(int offset, bool more, List<CityTrades.Row> rows)
    {
        _offset = offset;
        _more = more;
        _rows = rows;
    }

    public void Compose(IOutgoingPacket packet)
    {
        packet.WriteInteger(_offset);
        packet.WriteBoolean(_more);
        packet.WriteInteger(_rows.Count);
        foreach (var row in _rows)
        {
            packet.WriteInteger(row.Id);
            packet.WriteInteger(row.CreatedAt);
            packet.WriteInteger(row.UserA);
            packet.WriteString(row.NameA);
            packet.WriteInteger(row.UserB);
            packet.WriteString(row.NameB);
            packet.WriteString(row.RoomName);
            packet.WriteString(row.GaveA);
            packet.WriteString(row.GaveB);
        }
    }
}

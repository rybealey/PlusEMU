using Plus.HabboHotel.CityPanel;
using Plus.HabboHotel.GameClients;

namespace Plus.Communication.Packets.Outgoing.CityPanel;

/// <summary>
/// pixelrp City Panel: a page of the Global Ledger - the offset it starts at,
/// whether another follows, what the city holds right now, then the rows,
/// newest first. Figures are clamped to the wire's 32-bit integers.
/// </summary>
public class RpCityLedgerComposer : IServerPacket
{
    private readonly int _offset;
    private readonly bool _more;
    private readonly CityLedger.Totals _totals;
    private readonly List<CityLedger.Row> _rows;

    public uint MessageId => ServerPacketHeader.RpCityLedgerComposer;

    public RpCityLedgerComposer(int offset, bool more, CityLedger.Totals totals, List<CityLedger.Row> rows)
    {
        _offset = offset;
        _more = more;
        _totals = totals;
        _rows = rows;
    }

    private static int Clamp(long value) => (int)Math.Clamp(value, int.MinValue, int.MaxValue);

    public void Compose(IOutgoingPacket packet)
    {
        packet.WriteInteger(_offset);
        packet.WriteBoolean(_more);
        packet.WriteInteger(Clamp(_totals.Hand));
        packet.WriteInteger(Clamp(_totals.Checking));
        packet.WriteInteger(Clamp(_totals.Savings));
        packet.WriteInteger(_totals.Accounts);
        packet.WriteInteger(_rows.Count);
        foreach (var row in _rows)
        {
            packet.WriteInteger(row.CreatedAt);
            packet.WriteInteger(row.UserId);
            packet.WriteString(row.Username);
            packet.WriteString(row.Account);
            packet.WriteString(row.Kind);
            packet.WriteInteger(Clamp(row.Amount));
            packet.WriteInteger(Clamp(row.BalanceAfter));
            packet.WriteString(row.Source);
        }
    }
}

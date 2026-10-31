using Plus.HabboHotel.GameClients;

namespace Plus.Communication.Packets.Outgoing.Corporations;

/// <summary>
/// pixelrp Corporation Panel: Weekly shifts - every employee of the
/// corporation with seconds worked on each UK day of one week (Monday first)
/// and the week's total. The client turns seconds into 10-minute shifts.
/// </summary>
public class RpCorpPanelShiftsComposer : IServerPacket
{
    public record Row(int UserId, string Username, string Look, string Gender, string RankName, int RankOrder, int Tier, int Tiers,
        bool OnDuty, bool Online, int WeekSeconds, int[] DaySeconds);

    private readonly int _weeksAgo;
    private readonly int _weekStartedAt;
    private readonly int _weekResetAt;
    private readonly int _shiftQuota;
    private readonly List<Row> _rows;

    public uint MessageId => ServerPacketHeader.RpCorpPanelShiftsComposer;

    public RpCorpPanelShiftsComposer(int weeksAgo, int weekStartedAt, int weekResetAt, int shiftQuota, List<Row> rows)
    {
        _weeksAgo = weeksAgo;
        _weekStartedAt = weekStartedAt;
        _weekResetAt = weekResetAt;
        _shiftQuota = shiftQuota;
        _rows = rows;
    }

    public void Compose(IOutgoingPacket packet)
    {
        packet.WriteInteger(_weeksAgo);
        packet.WriteInteger(_weekStartedAt);
        packet.WriteInteger(_weekResetAt);
        packet.WriteInteger(_shiftQuota);
        packet.WriteInteger(_rows.Count);
        foreach (var row in _rows)
        {
            packet.WriteInteger(row.UserId);
            packet.WriteString(row.Username ?? "");
            packet.WriteString(row.Look ?? "");
            packet.WriteString(row.Gender ?? "");
            packet.WriteString(row.RankName ?? "");
            packet.WriteInteger(row.RankOrder);
            packet.WriteInteger(row.Tier);
            packet.WriteInteger(row.Tiers);
            packet.WriteBoolean(row.OnDuty);
            packet.WriteBoolean(row.Online);
            packet.WriteInteger(row.WeekSeconds);
            for (var i = 0; i < 7; i++)
                packet.WriteInteger(i < row.DaySeconds.Length ? row.DaySeconds[i] : 0);
        }
    }
}

using Plus.HabboHotel.GameClients;

namespace Plus.Communication.Packets.Outgoing.Corporations;

/// <summary>
/// pixelrp Corporation Panel: Dismissals - everyone who left in the period
/// (30, 90 or 0 = all days), newest first: their last rank, fired / quit /
/// staff, why, by whom, when they were hired and left, and whether they can be
/// hired again (the blacklist: eligible, until a date, or never).
/// </summary>
public class RpCorpPanelDismissalsComposer : IServerPacket
{
    public record Row(int Id, int UserId, string Username, string Look, string Gender, string RankTitle, string Kind, string Reason, string ActorName,
        int HiredAt, int CreatedAt, string Rehire, int RehireAt);

    private readonly int _days;
    private readonly List<Row> _rows;

    public uint MessageId => ServerPacketHeader.RpCorpPanelDismissalsComposer;

    public RpCorpPanelDismissalsComposer(int days, List<Row> rows)
    {
        _days = days;
        _rows = rows;
    }

    public void Compose(IOutgoingPacket packet)
    {
        packet.WriteInteger(_days);
        packet.WriteInteger(_rows.Count);
        foreach (var row in _rows)
        {
            packet.WriteInteger(row.Id);
            packet.WriteInteger(row.UserId);
            packet.WriteString(row.Username ?? "");
            packet.WriteString(row.Look ?? "");
            packet.WriteString(row.Gender ?? "");
            packet.WriteString(row.RankTitle ?? "");
            packet.WriteString(row.Kind ?? "");
            packet.WriteString(row.Reason ?? "");
            packet.WriteString(row.ActorName ?? "");
            packet.WriteInteger(row.HiredAt);
            packet.WriteInteger(row.CreatedAt);
            packet.WriteString(row.Rehire ?? "");
            packet.WriteInteger(row.RehireAt);
        }
    }
}

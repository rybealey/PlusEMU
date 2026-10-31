using Plus.HabboHotel.GameClients;

namespace Plus.Communication.Packets.Outgoing.Corporations;

/// <summary>
/// pixelrp Corporation Panel: LOA tracking - whether the viewer approves,
/// today and this week's Monday (UK, yyyy-MM-dd) to lay the timeline on, every
/// LOA waiting for a decision or still within the weeks shown, and every
/// employee with whether the viewer may log leave for them.
/// </summary>
public class RpCorpPanelLoaComposer : IServerPacket
{
    public record Entry(int Id, int UserId, string Username, string Look, string Gender, string RankTitle, string StartsOn, string EndsOn,
        string Reason, string Status, string RequestedByName, bool AskedThemselves, string DecidedByName, int CreatedAt);

    public record Person(int UserId, string Username, string RankTitle, bool CanLog);

    private readonly bool _canApprove;
    private readonly string _today;
    private readonly string _monday;
    private readonly List<Entry> _entries;
    private readonly List<Person> _people;

    public uint MessageId => ServerPacketHeader.RpCorpPanelLoaComposer;

    public RpCorpPanelLoaComposer(bool canApprove, string today, string monday, List<Entry> entries, List<Person> people)
    {
        _canApprove = canApprove;
        _today = today;
        _monday = monday;
        _entries = entries;
        _people = people;
    }

    public void Compose(IOutgoingPacket packet)
    {
        packet.WriteBoolean(_canApprove);
        packet.WriteString(_today);
        packet.WriteString(_monday);
        packet.WriteInteger(_entries.Count);
        foreach (var entry in _entries)
        {
            packet.WriteInteger(entry.Id);
            packet.WriteInteger(entry.UserId);
            packet.WriteString(entry.Username ?? "");
            packet.WriteString(entry.Look ?? "");
            packet.WriteString(entry.Gender ?? "");
            packet.WriteString(entry.RankTitle ?? "");
            packet.WriteString(entry.StartsOn);
            packet.WriteString(entry.EndsOn);
            packet.WriteString(entry.Reason ?? "");
            packet.WriteString(entry.Status ?? "");
            packet.WriteString(entry.RequestedByName ?? "");
            packet.WriteBoolean(entry.AskedThemselves);
            packet.WriteString(entry.DecidedByName ?? "");
            packet.WriteInteger(entry.CreatedAt);
        }
        packet.WriteInteger(_people.Count);
        foreach (var person in _people)
        {
            packet.WriteInteger(person.UserId);
            packet.WriteString(person.Username ?? "");
            packet.WriteString(person.RankTitle ?? "");
            packet.WriteBoolean(person.CanLog);
        }
    }
}

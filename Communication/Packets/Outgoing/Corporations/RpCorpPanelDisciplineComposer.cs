using Plus.HabboHotel.Corporations;
using Plus.HabboHotel.GameClients;

namespace Plus.Communication.Packets.Outgoing.Corporations;

/// <summary>
/// pixelrp Corporation Panel: Warnings and Strikes for one week - the rules
/// (warnings per strike, strikes for a review), every live warning and strike
/// newest first, and every employee with their counts that week and whether
/// the viewer may discipline them (the issue form's list).
/// </summary>
public class RpCorpPanelDisciplineComposer : IServerPacket
{
    public record Person(int UserId, string Username, string Look, string Gender, string RankName, int Tier, int Warnings, int Strikes, bool CanDiscipline);

    private readonly int _weeksAgo;
    private readonly int _weekStartedAt;
    private readonly int _weekResetAt;
    private readonly int _warningsPerStrike;
    private readonly int _strikesForReview;
    private readonly List<CorpDiscipline.Entry> _entries;
    private readonly List<Person> _people;

    public uint MessageId => ServerPacketHeader.RpCorpPanelDisciplineComposer;

    public RpCorpPanelDisciplineComposer(int weeksAgo, int weekStartedAt, int weekResetAt, int warningsPerStrike, int strikesForReview,
        List<CorpDiscipline.Entry> entries, List<Person> people)
    {
        _weeksAgo = weeksAgo;
        _weekStartedAt = weekStartedAt;
        _weekResetAt = weekResetAt;
        _warningsPerStrike = warningsPerStrike;
        _strikesForReview = strikesForReview;
        _entries = entries;
        _people = people;
    }

    public void Compose(IOutgoingPacket packet)
    {
        packet.WriteInteger(_weeksAgo);
        packet.WriteInteger(_weekStartedAt);
        packet.WriteInteger(_weekResetAt);
        packet.WriteInteger(_warningsPerStrike);
        packet.WriteInteger(_strikesForReview);
        packet.WriteInteger(_entries.Count);
        foreach (var entry in _entries)
            WriteEntry(packet, entry);
        packet.WriteInteger(_people.Count);
        foreach (var person in _people)
        {
            packet.WriteInteger(person.UserId);
            packet.WriteString(person.Username ?? "");
            packet.WriteString(person.Look ?? "");
            packet.WriteString(person.Gender ?? "");
            packet.WriteString(person.RankName ?? "");
            packet.WriteInteger(person.Tier);
            packet.WriteInteger(person.Warnings);
            packet.WriteInteger(person.Strikes);
            packet.WriteBoolean(person.CanDiscipline);
        }
    }

    /// <summary>One warning or strike, as this composer and the employee file both write it.</summary>
    public static void WriteEntry(IOutgoingPacket packet, CorpDiscipline.Entry entry)
    {
        packet.WriteInteger(entry.Id);
        packet.WriteString(entry.Kind ?? "");
        packet.WriteInteger(entry.UserId);
        packet.WriteString(entry.Username ?? "");
        packet.WriteString(entry.Look ?? "");
        packet.WriteString(entry.Gender ?? "");
        packet.WriteString(entry.RankName ?? "");
        packet.WriteString(entry.Reason ?? "");
        packet.WriteString(entry.Details ?? "");
        packet.WriteInteger(entry.IssuedBy);
        packet.WriteString(entry.IssuedByName ?? "");
        packet.WriteBoolean(entry.Automatic == 1);
        packet.WriteInteger(entry.CreatedAt);
        packet.WriteString(entry.RevokedByName ?? "");
        packet.WriteInteger(entry.RevokedAt);
    }
}

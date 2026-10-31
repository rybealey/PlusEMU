using Plus.HabboHotel.GameClients;

namespace Plus.Communication.Packets.Outgoing.Corporations;

/// <summary>
/// pixelrp Corporation Panel: the Dashboard for one week - headcount, who is
/// on duty now, shift seconds worked and per UK day, how many met the quota,
/// and the management ranks (panel_access) with their week. Later slices
/// append their counts (warnings, strikes, queues) after the leaders.
/// </summary>
public class RpCorpPanelDashboardComposer : IServerPacket
{
    public record Leader(int UserId, string Username, string Look, string Gender, string RankName, bool OnDuty, bool Online, int WeekSeconds);

    private readonly int _weeksAgo;
    private readonly int _weekStartedAt;
    private readonly int _weekResetAt;
    private readonly int _employees;
    private readonly int _onDuty;
    private readonly int _shiftSeconds;
    private readonly int _quotaMet;
    private readonly int _shiftQuota;
    private readonly int[] _daySeconds;
    private readonly List<Leader> _leaders;
    private readonly int _warnings;
    private readonly int _strikes;
    private readonly int _reviews;
    private readonly (int Promotions, int Demotions) _pending;

    public uint MessageId => ServerPacketHeader.RpCorpPanelDashboardComposer;

    public RpCorpPanelDashboardComposer(int weeksAgo, int weekStartedAt, int weekResetAt, int employees, int onDuty, int shiftSeconds,
        int quotaMet, int shiftQuota, int[] daySeconds, List<Leader> leaders, int warnings, int strikes, int reviews,
        (int Promotions, int Demotions) pending)
    {
        _weeksAgo = weeksAgo;
        _weekStartedAt = weekStartedAt;
        _weekResetAt = weekResetAt;
        _employees = employees;
        _onDuty = onDuty;
        _shiftSeconds = shiftSeconds;
        _quotaMet = quotaMet;
        _shiftQuota = shiftQuota;
        _daySeconds = daySeconds;
        _leaders = leaders;
        _warnings = warnings;
        _strikes = strikes;
        _reviews = reviews;
        _pending = pending;
    }

    public void Compose(IOutgoingPacket packet)
    {
        packet.WriteInteger(_weeksAgo);
        packet.WriteInteger(_weekStartedAt);
        packet.WriteInteger(_weekResetAt);
        packet.WriteInteger(_employees);
        packet.WriteInteger(_onDuty);
        packet.WriteInteger(_shiftSeconds);
        packet.WriteInteger(_quotaMet);
        packet.WriteInteger(_shiftQuota);
        for (var i = 0; i < 7; i++)
            packet.WriteInteger(i < _daySeconds.Length ? _daySeconds[i] : 0);
        packet.WriteInteger(_leaders.Count);
        foreach (var leader in _leaders)
        {
            packet.WriteInteger(leader.UserId);
            packet.WriteString(leader.Username ?? "");
            packet.WriteString(leader.Look ?? "");
            packet.WriteString(leader.Gender ?? "");
            packet.WriteString(leader.RankName ?? "");
            packet.WriteBoolean(leader.OnDuty);
            packet.WriteBoolean(leader.Online);
            packet.WriteInteger(leader.WeekSeconds);
        }
        // slice 2: the week's live warnings and strikes, and how many
        // employees are up for a dismissal review
        packet.WriteInteger(_warnings);
        packet.WriteInteger(_strikes);
        packet.WriteInteger(_reviews);
        // slice 3: open promotion and demotion requests
        packet.WriteInteger(_pending.Promotions);
        packet.WriteInteger(_pending.Demotions);
    }
}

using Plus.HabboHotel.Corporations;
using Plus.HabboHotel.GameClients;

namespace Plus.Communication.Packets.Outgoing.Corporations;

/// <summary>
/// pixelrp Corporation Panel: one employee's file - who they are, when they
/// were hired and last online, this week's shifts, warnings and strikes,
/// whether the viewer may discipline them, shift seconds for the last weeks
/// (oldest first, from firstWeekStartedAt), and their record newest first.
/// </summary>
public class RpCorpPanelEmployeeComposer : IServerPacket
{
    private readonly int _userId;
    private readonly string _username;
    private readonly string _look;
    private readonly string _gender;
    private readonly string _rankName;
    private readonly int _tier;
    private readonly int _hiredAt;
    private readonly int _lastOnline;
    private readonly bool _onDuty;
    private readonly bool _online;
    private readonly int _shiftQuota;
    private readonly int _warnings;
    private readonly int _strikes;
    private readonly bool _canDiscipline;
    private readonly int _firstWeekStartedAt;
    private readonly int[] _weekSeconds;
    private readonly List<CorpDiscipline.Entry> _record;
    private readonly List<CorpRanks.Change> _rankChanges;
    private readonly string _loaUntil;

    public uint MessageId => ServerPacketHeader.RpCorpPanelEmployeeComposer;

    public RpCorpPanelEmployeeComposer(int userId, string username, string look, string gender, string rankName, int tier, int hiredAt, int lastOnline,
        bool onDuty, bool online, int shiftQuota, int warnings, int strikes, bool canDiscipline, int firstWeekStartedAt, int[] weekSeconds,
        List<CorpDiscipline.Entry> record, List<CorpRanks.Change> rankChanges, string loaUntil)
    {
        _userId = userId;
        _username = username ?? "";
        _look = look ?? "";
        _gender = gender ?? "";
        _rankName = rankName ?? "";
        _tier = tier;
        _hiredAt = hiredAt;
        _lastOnline = lastOnline;
        _onDuty = onDuty;
        _online = online;
        _shiftQuota = shiftQuota;
        _warnings = warnings;
        _strikes = strikes;
        _canDiscipline = canDiscipline;
        _firstWeekStartedAt = firstWeekStartedAt;
        _weekSeconds = weekSeconds;
        _record = record;
        _rankChanges = rankChanges;
        _loaUntil = loaUntil ?? "";
    }

    public void Compose(IOutgoingPacket packet)
    {
        packet.WriteInteger(_userId);
        packet.WriteString(_username);
        packet.WriteString(_look);
        packet.WriteString(_gender);
        packet.WriteString(_rankName);
        packet.WriteInteger(_tier);
        packet.WriteInteger(_hiredAt);
        packet.WriteInteger(_lastOnline);
        packet.WriteBoolean(_onDuty);
        packet.WriteBoolean(_online);
        packet.WriteInteger(_shiftQuota);
        packet.WriteInteger(_warnings);
        packet.WriteInteger(_strikes);
        packet.WriteBoolean(_canDiscipline);
        packet.WriteInteger(_firstWeekStartedAt);
        packet.WriteInteger(_weekSeconds.Length);
        foreach (var seconds in _weekSeconds)
            packet.WriteInteger(seconds);
        packet.WriteInteger(_record.Count);
        foreach (var entry in _record)
            RpCorpPanelDisciplineComposer.WriteEntry(packet, entry);
        // slice 3: promotions and demotions, newest first
        packet.WriteInteger(_rankChanges.Count);
        foreach (var change in _rankChanges)
            RpCorpPanelRankLogComposer.WriteChange(packet, change);
        // slice 4: the last day of leave they're on now (UK, yyyy-MM-dd), or ""
        packet.WriteString(_loaUntil);
    }
}

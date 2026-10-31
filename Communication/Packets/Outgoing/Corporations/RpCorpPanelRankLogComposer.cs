using Plus.HabboHotel.Corporations;
using Plus.HabboHotel.GameClients;

namespace Plus.Communication.Packets.Outgoing.Corporations;

/// <summary>
/// pixelrp Corporation Panel: the Promotions and Demotions logs for one week -
/// every step up or down the ladder, newest first, with who made it, how
/// (command or approved request) and the employee's week at the time. The
/// client splits it into the two pages.
/// </summary>
public class RpCorpPanelRankLogComposer : IServerPacket
{
    private readonly int _weeksAgo;
    private readonly int _weekStartedAt;
    private readonly int _weekResetAt;
    private readonly List<CorpRanks.Change> _changes;

    public uint MessageId => ServerPacketHeader.RpCorpPanelRankLogComposer;

    public RpCorpPanelRankLogComposer(int weeksAgo, int weekStartedAt, int weekResetAt, List<CorpRanks.Change> changes)
    {
        _weeksAgo = weeksAgo;
        _weekStartedAt = weekStartedAt;
        _weekResetAt = weekResetAt;
        _changes = changes;
    }

    public void Compose(IOutgoingPacket packet)
    {
        packet.WriteInteger(_weeksAgo);
        packet.WriteInteger(_weekStartedAt);
        packet.WriteInteger(_weekResetAt);
        packet.WriteInteger(_changes.Count);
        foreach (var change in _changes)
            WriteChange(packet, change);
    }

    /// <summary>One rank change, as this composer and the employee file both write it.</summary>
    public static void WriteChange(IOutgoingPacket packet, CorpRanks.Change change)
    {
        packet.WriteInteger(change.Id);
        packet.WriteInteger(change.UserId);
        packet.WriteString(change.Username ?? "");
        packet.WriteString(change.Look ?? "");
        packet.WriteString(change.Gender ?? "");
        packet.WriteString(change.Direction ?? "");
        packet.WriteString(CorpRanks.Title(change.FromRankName, change.FromTier));
        packet.WriteString(CorpRanks.Title(change.ToRankName, change.ToTier));
        packet.WriteString(change.Reason ?? "");
        packet.WriteString(change.ActorName ?? "");
        packet.WriteString(change.Source ?? "");
        packet.WriteString(change.RequestedByName ?? "");
        packet.WriteInteger(change.ShiftSecondsWeek);
        packet.WriteInteger(change.Warnings);
        packet.WriteInteger(change.Strikes);
        packet.WriteInteger(change.CreatedAt);
    }
}

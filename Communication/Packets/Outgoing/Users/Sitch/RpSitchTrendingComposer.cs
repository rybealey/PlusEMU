using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Sitch;

namespace Plus.Communication.Packets.Outgoing.Users.Sitch;

/// <summary>
/// pixelrp: what the city is talking about, for the Search tab's resting state.
///
/// Carries canModerate so the suppress button can be drawn. The client hiding
/// it is a courtesy; RpSitchSuppressTagEvent checks the permission itself,
/// because a button nobody can see is not a gate.
/// </summary>
public class RpSitchTrendingComposer : IServerPacket
{
    private readonly List<SitchUtility.TrendRow> _tags;
    private readonly List<SitchUtility.TalkedAboutRow> _people;
    private readonly bool _canModerate;

    public uint MessageId => ServerPacketHeader.RpSitchTrendingComposer;

    public RpSitchTrendingComposer(List<SitchUtility.TrendRow> tags,
        List<SitchUtility.TalkedAboutRow> people, bool canModerate)
    {
        _tags = tags;
        _people = people;
        _canModerate = canModerate;
    }

    public void Compose(IOutgoingPacket packet)
    {
        // An int, like every other canModerate on this app's wire.
        packet.WriteInteger(_canModerate ? 1 : 0);
        packet.WriteInteger(_tags.Count);
        foreach (var t in _tags)
        {
            packet.WriteString(t.Tag ?? "");
            packet.WriteInteger(t.Posts);
        }
        packet.WriteInteger(_people.Count);
        foreach (var p in _people)
        {
            packet.WriteInteger(p.UserId);
            packet.WriteString(p.Username ?? "");
            packet.WriteString(p.Figure ?? "");
            packet.WriteInteger(p.Mentions);
        }
    }
}

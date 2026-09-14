using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Sitch;

namespace Plus.Communication.Packets.Outgoing.Users.Sitch;

/// <summary>
/// pixelrp: Sitch search results - people, then posts.
///
/// The query rides back so a slow answer cannot overwrite a newer one, the same
/// guard the catalog search uses.
/// </summary>
public class RpSitchSearchComposer : IServerPacket
{
    private readonly string _query;
    private readonly List<SitchUtility.ProfileRow> _people;
    private readonly List<SitchUtility.PostRow> _posts;

    public uint MessageId => ServerPacketHeader.RpSitchSearchComposer;

    public RpSitchSearchComposer(string query, List<SitchUtility.ProfileRow> people, List<SitchUtility.PostRow> posts)
    {
        _query = query;
        _people = people;
        _posts = posts;
    }

    public void Compose(IOutgoingPacket packet)
    {
        packet.WriteString(_query ?? "");
        packet.WriteInteger(_people.Count);
        foreach (var person in _people)
        {
            packet.WriteInteger(person.UserId);
            packet.WriteString(person.Username ?? "");
            packet.WriteString(person.Figure ?? "");
            packet.WriteString(person.Bio ?? "");
            packet.WriteInteger(person.Followers);
            packet.WriteInteger(person.Follows);
        }
        packet.WriteInteger(_posts.Count);
        foreach (var post in _posts) SitchPostWriter.WritePost(packet, post);
    }
}

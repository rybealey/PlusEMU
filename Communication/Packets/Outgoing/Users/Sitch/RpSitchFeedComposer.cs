using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Sitch;

namespace Plus.Communication.Packets.Outgoing.Users.Sitch;

/// <summary>
/// pixelrp: a Sitch timeline.
///
/// `following` says which tab asked, so a slow answer cannot land in the wrong
/// one. `canModerate` rides along because the client has no other way to know
/// whether this viewer may remove somebody else's post - it is an affordance
/// flag only, and RpSitchDeleteEvent re-checks the permission before acting.
/// </summary>
public class RpSitchFeedComposer : IServerPacket
{
    private readonly bool _following;
    private readonly bool _canModerate;
    private readonly List<SitchUtility.PostRow> _posts;

    public uint MessageId => ServerPacketHeader.RpSitchFeedComposer;

    public RpSitchFeedComposer(bool following, bool canModerate, List<SitchUtility.PostRow> posts)
    {
        _following = following;
        _canModerate = canModerate;
        _posts = posts;
    }

    public void Compose(IOutgoingPacket packet)
    {
        packet.WriteInteger(_following ? 1 : 0);
        packet.WriteInteger(_canModerate ? 1 : 0);
        packet.WriteInteger(_posts.Count);
        foreach (var p in _posts) SitchPostWriter.WritePost(packet, p);
    }
}

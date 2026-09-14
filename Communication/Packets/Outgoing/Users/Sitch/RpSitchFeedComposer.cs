using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Sitch;

namespace Plus.Communication.Packets.Outgoing.Users.Sitch;

/// <summary>pixelrp: a Sitch timeline. `following` says which tab asked, so a slow answer cannot land in the wrong one.</summary>
public class RpSitchFeedComposer : IServerPacket
{
    private readonly bool _following;
    private readonly List<SitchUtility.PostRow> _posts;

    public uint MessageId => ServerPacketHeader.RpSitchFeedComposer;

    public RpSitchFeedComposer(bool following, List<SitchUtility.PostRow> posts)
    {
        _following = following;
        _posts = posts;
    }

    public void Compose(IOutgoingPacket packet)
    {
        packet.WriteInteger(_following ? 1 : 0);
        packet.WriteInteger(_posts.Count);
        foreach (var p in _posts) SitchPostWriter.WritePost(packet, p);
    }
}

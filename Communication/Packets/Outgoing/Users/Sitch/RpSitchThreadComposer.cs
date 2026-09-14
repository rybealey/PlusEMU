using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Sitch;

namespace Plus.Communication.Packets.Outgoing.Users.Sitch;

/// <summary>pixelrp: one Sitch post and its replies. The root comes first; everything after it is a reply, oldest first.</summary>
public class RpSitchThreadComposer : IServerPacket
{
    private readonly int _postId;
    private readonly List<SitchUtility.PostRow> _posts;

    public uint MessageId => ServerPacketHeader.RpSitchThreadComposer;

    public RpSitchThreadComposer(int postId, List<SitchUtility.PostRow> posts)
    {
        _postId = postId;
        _posts = posts;
    }

    public void Compose(IOutgoingPacket packet)
    {
        packet.WriteInteger(_postId);
        packet.WriteInteger(_posts.Count);
        foreach (var p in _posts) SitchPostWriter.WritePost(packet, p);
    }
}

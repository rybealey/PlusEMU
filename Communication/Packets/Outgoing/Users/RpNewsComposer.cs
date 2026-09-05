using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.News;

namespace Plus.Communication.Packets.Outgoing.Users;

/// <summary>pixelrp: the News feed for one viewer - whether they may post, and the latest stories in full (pinned first, then newest).</summary>
public class RpNewsComposer : IServerPacket
{
    private readonly bool _canPost;
    private readonly List<NewsUtility.PostRow> _posts;

    public uint MessageId => ServerPacketHeader.RpNewsComposer;

    public RpNewsComposer(bool canPost, List<NewsUtility.PostRow> posts)
    {
        _canPost = canPost;
        _posts = posts;
    }

    public void Compose(IOutgoingPacket packet)
    {
        packet.WriteInteger(_canPost ? 1 : 0);
        packet.WriteInteger(_posts.Count);
        foreach (var p in _posts)
        {
            packet.WriteInteger(p.Id);
            packet.WriteInteger(p.AuthorId);
            packet.WriteString(p.AuthorName ?? "");
            packet.WriteString(p.Category ?? "");
            packet.WriteString(p.Title ?? "");
            packet.WriteString(p.Body ?? "");
            packet.WriteString(p.Image ?? "");
            packet.WriteInteger(p.Pinned);
            packet.WriteInteger(p.CreatedAt);
            packet.WriteInteger(p.UpdatedAt);
        }
    }
}

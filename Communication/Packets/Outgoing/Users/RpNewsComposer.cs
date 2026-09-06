using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.News;

namespace Plus.Communication.Packets.Outgoing.Users;

/// <summary>pixelrp: the News feed for one viewer - their staff level (0 reader, 1 staff, 2 senior) and the latest stories in full (pinned first, then newest).</summary>
public class RpNewsComposer : IServerPacket
{
    private readonly int _staffLevel;
    private readonly NewsUtility.BylineRow _byline;
    private readonly List<NewsUtility.PostRow> _posts;

    public uint MessageId => ServerPacketHeader.RpNewsComposer;

    public RpNewsComposer(int staffLevel, NewsUtility.BylineRow byline, List<NewsUtility.PostRow> posts)
    {
        _staffLevel = staffLevel;
        _byline = byline;
        _posts = posts;
    }

    public void Compose(IOutgoingPacket packet)
    {
        packet.WriteInteger(_staffLevel);
        // the newsroom byline, so the composer can show who "Trina" is
        packet.WriteInteger(_byline.Id);
        packet.WriteString(_byline.Username ?? "");
        packet.WriteString(_byline.Figure ?? "");
        packet.WriteInteger(_posts.Count);
        foreach (var p in _posts)
        {
            // an anonymous story is shown as Trina's; staff also get the real writer
            var shown = p.Anonymous == 1;
            packet.WriteInteger(p.Id);
            packet.WriteInteger(shown ? _byline.Id : p.AuthorId);
            packet.WriteString(shown ? (_byline.Username ?? "") : (p.AuthorName ?? ""));
            packet.WriteString(shown ? (_byline.Figure ?? "") : (p.AuthorFigure ?? ""));
            packet.WriteString(p.Category ?? "");
            packet.WriteString(p.Title ?? "");
            packet.WriteString(p.Body ?? "");
            packet.WriteString(p.Image ?? "");
            packet.WriteInteger(p.Pinned);
            packet.WriteInteger(p.CreatedAt);
            packet.WriteInteger(p.UpdatedAt);
            packet.WriteInteger(shown ? 1 : 0);
            packet.WriteInteger(_staffLevel > 0 ? p.AuthorId : 0);
            packet.WriteString(_staffLevel > 0 ? (p.AuthorName ?? "") : "");
        }
    }
}

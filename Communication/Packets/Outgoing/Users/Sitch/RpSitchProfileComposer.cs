using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Sitch;

namespace Plus.Communication.Packets.Outgoing.Users.Sitch;

/// <summary>pixelrp: a Sitch profile - who they are, the one song they chose, and their own posts.</summary>
public class RpSitchProfileComposer : IServerPacket
{
    private readonly SitchUtility.ProfileRow _profile;
    private readonly List<SitchUtility.PostRow> _posts;

    public uint MessageId => ServerPacketHeader.RpSitchProfileComposer;

    public RpSitchProfileComposer(SitchUtility.ProfileRow profile, List<SitchUtility.PostRow> posts)
    {
        _profile = profile;
        _posts = posts;
    }

    public void Compose(IOutgoingPacket packet)
    {
        packet.WriteInteger(_profile.UserId);
        packet.WriteString(_profile.Username ?? "");
        packet.WriteString(_profile.Figure ?? "");
        packet.WriteString(_profile.Motto ?? "");
        packet.WriteString(_profile.Bio ?? "");
        // The video id is the identity; title and author are what oEmbed said
        // when it was saved. No duration - oEmbed does not return one.
        packet.WriteString(_profile.FavoriteVideoId ?? "");
        packet.WriteString(_profile.FavoriteTitle ?? "");
        packet.WriteString(_profile.FavoriteAuthor ?? "");
        packet.WriteInteger(_profile.Followers);
        packet.WriteInteger(_profile.Following);
        packet.WriteInteger(_profile.Follows);
        packet.WriteInteger(_posts.Count);
        foreach (var p in _posts) SitchPostWriter.WritePost(packet, p);
    }
}

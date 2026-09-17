using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Sitch;

namespace Plus.Communication.Packets.Outgoing.Users.Sitch;

/// <summary>pixelrp: the one place that knows a Sitch post's wire shape.</summary>
internal static class SitchPostWriter
{
    /// <summary>
    /// One post on the wire. Kept in one place because four composers send the
    /// same shape and a field added to only three of them is a silent
    /// misalignment - the client reads positionally.
    /// </summary>
    internal static void WritePost(IOutgoingPacket packet, SitchUtility.PostRow p)
    {
        packet.WriteInteger(p.Id);
        packet.WriteInteger(p.ParentId);
        packet.WriteInteger(p.UserId);
        packet.WriteString(p.Username ?? "");
        packet.WriteString(p.Figure ?? "");
        packet.WriteInteger(p.Rank);
        packet.WriteString(p.Body ?? "");
        packet.WriteInteger(p.PhotoId);
        packet.WriteString(p.PhotoUrl ?? "");
        packet.WriteString(p.PhotoRoom ?? "");
        packet.WriteInteger(p.CreatedAt);
        packet.WriteInteger(p.Replies);
        packet.WriteInteger(p.Likes);
        packet.WriteInteger(p.Reposts);
        packet.WriteInteger(p.Liked);
        packet.WriteInteger(p.Reposted);
        // Appended rather than slotted in beside the author, because the client
        // reads this positionally: anything inserted mid-record shifts every
        // field after it. Empty and 0 on every timeline but a profile.
        packet.WriteString(p.RepostedBy ?? "");
        packet.WriteInteger(p.RepostedAt);
    }
}

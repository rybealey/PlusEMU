using Plus.HabboHotel.GameClients;

namespace Plus.Communication.Packets.Outgoing.Camera;

/// <summary>
/// pixelrp: every photo album the player owns or is a member of, for the
/// phone Photos app's Collections tab. Cover is the album's latest photo
/// (empty when the album has none). Members are listed for shared albums so
/// the owner can manage them client-side.
/// </summary>
public class RpAlbumListComposer : IServerPacket
{
    public record Member(int Id, string Username);

    public record Album(int Id, string Name, bool Shared, int OwnerId, string OwnerName, int PhotoCount, string CoverUrl, List<Member> Members);

    private readonly List<Album> _albums;

    public uint MessageId => ServerPacketHeader.RpAlbumListComposer;

    public RpAlbumListComposer(List<Album> albums)
    {
        _albums = albums;
    }

    public void Compose(IOutgoingPacket packet)
    {
        packet.WriteInteger(_albums.Count);
        foreach (var album in _albums)
        {
            packet.WriteInteger(album.Id);
            packet.WriteString(album.Name);
            packet.WriteBoolean(album.Shared);
            packet.WriteInteger(album.OwnerId);
            packet.WriteString(album.OwnerName);
            packet.WriteInteger(album.PhotoCount);
            packet.WriteString(album.CoverUrl);
            packet.WriteInteger(album.Members.Count);
            foreach (var member in album.Members)
            {
                packet.WriteInteger(member.Id);
                packet.WriteString(member.Username);
            }
        }
    }
}

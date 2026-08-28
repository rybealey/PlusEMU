using Plus.HabboHotel.GameClients;

namespace Plus.Communication.Packets.Outgoing.Camera;

/// <summary>
/// pixelrp: the photos inside one album, newest-added first. Shared albums
/// mix photos from every contributor, so each entry carries its owner.
/// </summary>
public class RpAlbumPhotosComposer : IServerPacket
{
    public record Photo(int Id, string Url, int Timestamp, int OwnerId, string OwnerName);

    private readonly int _albumId;
    private readonly List<Photo> _photos;

    public uint MessageId => ServerPacketHeader.RpAlbumPhotosComposer;

    public RpAlbumPhotosComposer(int albumId, List<Photo> photos)
    {
        _albumId = albumId;
        _photos = photos;
    }

    public void Compose(IOutgoingPacket packet)
    {
        packet.WriteInteger(_albumId);
        packet.WriteInteger(_photos.Count);
        foreach (var photo in _photos)
        {
            packet.WriteInteger(photo.Id);
            packet.WriteString(photo.Url);
            packet.WriteInteger(photo.Timestamp);
            packet.WriteInteger(photo.OwnerId);
            packet.WriteString(photo.OwnerName);
        }
    }
}
